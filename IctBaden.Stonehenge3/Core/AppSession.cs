﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using IctBaden.Stonehenge3.Hosting;
using IctBaden.Stonehenge3.Resources;
using IctBaden.Stonehenge3.ViewModel;
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable EventNeverSubscribedTo.Global

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace IctBaden.Stonehenge3.Core
{
    public class AppSession : INotifyPropertyChanged
    {
        public string AppInstanceId { get; private set; }

        public string HostDomain { get; private set; }
        public bool IsLocal { get; private set; }
        public string ClientAddress { get; private set; }
        public string UserAgent { get; private set; }
        public string Platform { get; private set; }
        public string Browser { get; private set; }

        public bool CookiesSupported { get; private set; }
        public bool StonehengeCookieSet { get; private set; }
        public Dictionary<string, string> Cookies { get; private set; }

        public DateTime ConnectedSince { get; private set; }
        public DateTime LastAccess { get; private set; }
        public string Context { get; private set; }
        public DateTime LastUserAction { get; private set; }

        private readonly Guid _id;
        public string Id => $"{_id:N}";

        public string PermanentSessionId { get; private set; }

        private readonly int _eventTimeoutMs;
        private readonly List<string> _events = new List<string>();
        private readonly AutoResetEvent _eventRelease = new AutoResetEvent(false);

        public bool IsWaitingForEvents { get; private set; }

        public bool SecureCookies { get; private set; }

        public List<string> CollectEvents()
        {
            IsWaitingForEvents = true;
            _eventRelease.WaitOne(TimeSpan.FromMilliseconds(_eventTimeoutMs));
            // wait for maximum 500ms for more events - if there is none within 100ms - continue
            var max = 50;
            while (_eventRelease.WaitOne(100) && (max > 0))
            {
                max--;
            }
            IsWaitingForEvents = false;
            var events = _events.Select(e => e).ToList();
            _events.Clear();
            return events;
        }

        private object _viewModel;
        public object ViewModel
        {
            get => _viewModel;
            set
            {
                (_viewModel as IDisposable)?.Dispose();

                _viewModel = value;
                if (value is INotifyPropertyChanged npc)
                {
                    npc.PropertyChanged += (sender, args) =>
                    {
                        if (!(sender is ActiveViewModel avm))
                        {
                            return;
                        }
                        lock (avm.Session._events)
                        {
                            avm.Session.EventAdd(args.PropertyName);
                        }
                    };
                }
            }
        }

        // ReSharper disable once UnusedMember.Global
        public void ClientAddressChanged(string address)
        {
            ClientAddress = address;
            NotifyPropertyChanged(nameof(ClientAddress));
        }

        internal object SetViewModelType(string typeName)
        {
            var oldViewModel = ViewModel;
            if (oldViewModel != null)
            {
                if ((oldViewModel.GetType().FullName == typeName))
                    return oldViewModel;

                var disposable = oldViewModel as IDisposable;
                disposable?.Dispose();
            }

            var resourceLoader = _resourceLoader.Loaders.First(ld => ld.GetType() == typeof(ResourceLoader)) as ResourceLoader;
            if(resourceLoader == null)
            {
                ViewModel = null;
                Debug.WriteLine("Could not create ViewModel - No resourceLoader specified:" + typeName);
                return null;
            }

            var newViewModelType = resourceLoader.ResourceAssemblies
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(type => type.FullName?.EndsWith(typeName) ?? false);

            if (newViewModelType == null)
            {
                ViewModel = null;
                Debug.WriteLine("Could not create ViewModel:" + typeName);
                return null;
            }

            ViewModel = CreateType(newViewModelType);
            return ViewModel;
        }

        private object CreateType(Type type)
        {
            object instance = null;
            foreach (var constructor in type.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    instance = Activator.CreateInstance(type);
                    break;
                }

                var paramValues = new object[parameters.Length];

                for (var ix = 0; ix < parameters.Length; ix++)
                {
                    var parameterInfo = parameters[ix];
                    if (parameterInfo.ParameterType == typeof(AppSession))
                    {
                        paramValues[ix] = this;
                    }
                    else
                    {
                        paramValues[ix] = _resourceLoader.Services.GetService(parameterInfo.ParameterType)
                                          ?? CreateType(parameterInfo.ParameterType);
                    }
                }

                try
                {
                    instance = Activator.CreateInstance(type, paramValues);
                    break;
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message);
                }
            }

            return instance;
        }


        public string SubDomain
        {
            get
            {
                if (string.IsNullOrEmpty(HostDomain))
                    return string.Empty;

                var parts = HostDomain.Split('.');
                if (parts.Length == 1) return string.Empty;

                var isNumeric = int.TryParse(parts[0], out _);
                return isNumeric ? HostDomain : parts[0];
            }
        }

        private readonly Dictionary<string, object> _userData;
        public object this[string key]
        {
            get => _userData.ContainsKey(key) ? _userData[key] : null;
            set
            {
                if (this[key] == value)
                    return;
                _userData[key] = value;
                NotifyPropertyChanged(key);
            }
        }

        public void Set<T>(string key, T value)
        {
            _userData[key] = value;
        }
        public T Get<T>(string key)
        {
            if (!_userData.ContainsKey(key))
                return default(T);

            return (T)_userData[key];
        }
        public void Remove(string key)
        {
            _userData.Remove(key);
        }

        public TimeSpan ConnectedDuration => DateTime.Now - ConnectedSince;

        public TimeSpan LastAccessDuration => DateTime.Now - LastAccess;

        // ReSharper disable once UnusedMember.Global
        public TimeSpan LastUserActionDuration => DateTime.Now - LastUserAction;

        public event Action TimedOut;
        private Timer _pollSessionTimeout;
        public TimeSpan SessionTimeout { get; private set; }
        public bool IsTimedOut => LastAccessDuration > SessionTimeout;

        // ReSharper disable once UnusedMember.Global
        public void SetTimeout(TimeSpan timeout)
        {
            _pollSessionTimeout?.Dispose();
            SessionTimeout = timeout;
            if (Math.Abs(timeout.TotalMilliseconds) > 0.1)
            {
                _pollSessionTimeout = new Timer(CheckSessionTimeout, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
        }

        private void CheckSessionTimeout(object _)
        {
            if ((LastAccessDuration > SessionTimeout) && (_terminator != null))
            {
                _pollSessionTimeout.Dispose();
                _terminator.Dispose();
                TimedOut?.Invoke();
            }
            NotifyPropertyChanged(nameof(ConnectedDuration));
            NotifyPropertyChanged(nameof(LastAccessDuration));
        }

        private IDisposable _terminator;


        // ReSharper disable once UnusedMember.Global
        public void SetTerminator(IDisposable disposable)
        {
            _terminator = disposable;
        }

        private readonly StonehengeResourceLoader _resourceLoader;

        public AppSession()
            : this(null, new StonehengeHostOptions())
        {
        }
        public AppSession(StonehengeResourceLoader resourceLoader, StonehengeHostOptions options)
        {
            if (resourceLoader == null)
            {
                var assemblies = new List<Assembly>
                    {
                        Assembly.GetEntryAssembly(),
                        Assembly.GetExecutingAssembly(),
                        Assembly.GetAssembly(typeof(ResourceLoader))
                    }
                    .Distinct()
                    .ToList();

                var loader = new ResourceLoader(assemblies, Assembly.GetCallingAssembly());
                resourceLoader = new StonehengeResourceLoader(new List<IStonehengeResourceProvider>{ loader });
            }

            _resourceLoader = resourceLoader;
            _userData = new Dictionary<string, object>();
            _id = Guid.NewGuid();
            AppInstanceId = Guid.NewGuid().ToString("N");
            SessionTimeout = TimeSpan.FromMinutes(15);
            Cookies = new Dictionary<string, string>();
            LastAccess = DateTime.Now;
            
            _eventTimeoutMs = options.GetEventTimeoutMs();

            try
            {
                if (Assembly.GetEntryAssembly() == null) return;
                var path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? ".";
                var cfg = Path.Combine(path, "Stonehenge3.cfg");
                if (!File.Exists(cfg)) return;

                var settings = File.ReadAllLines(cfg);
                var secureCookies = settings.FirstOrDefault(s => s.Contains("SecureCookies"));
                if (secureCookies != null)
                {
                    var set = secureCookies.Split('=');
                    SecureCookies = (set.Length > 1) && (set[1].Trim() == "1");
                }
            }
            catch
            {
                // ignore
            }
        }

        // ReSharper disable once UnusedMember.Global
        public bool IsInitialized => UserAgent != null;

        public void Initialize(string hostDomain, bool isLocal, string clientAddress, string userAgent)
        {
            HostDomain = hostDomain;
            IsLocal = isLocal;
            ClientAddress = clientAddress;
            UserAgent = userAgent;
            ConnectedSince = DateTime.Now;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                DetectBrowser(userAgent);
            }
        }

        // ReSharper disable once UnusedParameter.Local
        private void DetectBrowser(string userAgent)
        {
            // Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.87 Safari/537.36
            //TODO: Decocder
            Browser = "";
            CookiesSupported = true;
            Platform = "OS";
        }

        public void Accessed(IDictionary<string, string> cookies, bool userAction)
        {
            foreach (var cookie in cookies)
            {
                if (Cookies.ContainsKey(cookie.Key))
                {
                    Cookies[cookie.Key] = cookie.Value;
                }
                else
                {
                    Cookies.Add(cookie.Key, cookie.Value);
                }
            }


            if ((PermanentSessionId == null) && cookies.ContainsKey("ss-pid"))
            {
                PermanentSessionId = cookies["ss-pid"];
            }
            LastAccess = DateTime.Now;
            NotifyPropertyChanged(nameof(LastAccess));
            if (userAction)
            {
                LastUserAction = DateTime.Now;
                NotifyPropertyChanged(nameof(LastUserAction));
            }
            StonehengeCookieSet = cookies.ContainsKey("stonehenge-id");
            NotifyPropertyChanged(nameof(StonehengeCookieSet));
        }

        public void SetContext(string context)
        {
            Context = context;
        }

        public void EventsClear(bool forceEnd)
        {
            lock (_events)
            {
                //var privateEvents = Events.Where(e => e.StartsWith(AppService.PropertyNameId)).ToList();
                _events.Clear();
                //Events.AddRange(privateEvents);
                if (forceEnd)
                {
                    _eventRelease.Set();
                    _eventRelease.Set();
                }
            }
        }

        public void EventAdd(string name)
        {
            lock (_events)
            {
                if (!_events.Contains(name))
                    _events.Add(name);

                _eventRelease.Set();
            }
        }

        public string GetResourceETag(string path)
        {
            return AppInstanceId + path.GetHashCode().ToString("x8");
        }

        public override string ToString()
        {
            // ReSharper disable once UseStringInterpolation
            return string.Format("[{0}] {1} {2}", Id, ConnectedSince.ToShortDateString() + " " + ConnectedSince.ToShortTimeString(), SubDomain);
        }

        public event PropertyChangedEventHandler PropertyChanged;


        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}