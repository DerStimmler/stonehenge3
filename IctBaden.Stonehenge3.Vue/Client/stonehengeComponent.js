

stonehengeViewModelName = function component() {


    let vm = {

        StonehengeCancelRequests: function () {
            // no clue how to abort here
            //for (var rq = 0; rq < app.$http.pendingRequests.length; rq++) {
            //app.$http.pendingRequests[rq].abort();
            //}
            this.StonehengePollEventsActive = null;
        },

        StonehengeSetViewModelData: function (vmdata) {
            for (var propertyName in vmdata) {
                if (propertyName === "StonehengeNavigate") {
                    var target = vmdata[propertyName];
                    if (target.startsWith('#')) {
                        try {
                            document.getElementById(target.substring(1))
                                .scrollIntoView({ block: 'end', behaviour: 'smooth' });
                        } catch (error) {
                            // ignore
                            if (console && console.log) {
                                console.log("error: " + error);
                            }
                        }
                    } else {
                        app.$router.push(target);
                    }
                } else if (propertyName === "StonehengeEval") {
                    try {
                        var script = vmdata[propertyName];
                        eval(script);
                    } catch (error) {
                        // ignore
                        if (console && console.log) {
                            console.log("script: " + script);
                            console.log("error: " + error);
                        }
                    }
                } else {
                    //debugger;
                    this.model[propertyName] = vmdata[propertyName];
                }
            }
            if (!this.StonehengeInitialLoading) {
                if (typeof (app.stonehengeViewModelName.user_DataLoaded) == 'function') {
                    try {
                        app.stonehengeViewModelName.user_DataLoaded(this);
                    } catch (e) { }
                }
            }
        },

        StonehengePost: function (urlWithParams) {
            this.StonehengeCancelRequests();

            var props = ['propNames'];
            var formData = new Object();
            props.forEach(function (prop) {
                formData[prop] = app.stonehengeViewModelName.model[prop];
            });
            this.StonehengePostActive = true;
            app.$http.post(urlWithParams, JSON.stringify(formData))
                .then(response => {
                    let data = JSON.parse(response.bodyText);
                    this.StonehengeInitialLoading = false;
                    this.StonehengeIsLoading = false;
                    if (this.StonehengePostActive) {
                        this.StonehengeSetViewModelData(data);
                        this.StonehengePostActive = false;
                    }
                    if (this.StonehengePollEventsActive === null) {
                        setTimeout(function () { app.stonehengeViewModelName.StonehengePollEvents(true); }, this.StonehengePollDelay);
                    }
                })
                .catch(error => {
                    if (error.responseType !== "abort") {
                        this.StonehengeIsDisconnected = true;
                        //debugger;
                        window.location.reload();
                    }
                });
        },
        
        StonehengePollEvents: function (continuePolling) {
            if (!app.stonehengeViewModelName.model.StonehengeActive || app.stonehengeViewModelName.model.StonehengePostActive
                || app.stonehengeViewModelName.model.StonehengePollEventsActive !== null) return;
            var ts = new Date().getTime();
            app.stonehengeViewModelName.model.StonehengePollEventsActive = app.$http.get('/Events/stonehengeViewModelName?ts=' + ts)
                .then(response => {
                    let data = JSON.parse(response.bodyText);
                    app.stonehengeViewModelName.model.StonehengePollEventsActive = null;
                    app.stonehengeViewModelName.model.StonehengeIsDisconnected = false;
                    app.stonehengeViewModelName.StonehengeSetViewModelData(data);
                    if (continuePolling || app.stonehengeViewModelName.model.StonehengeContinuePolling) {
                        setTimeout(function () { app.stonehengeViewModelName.StonehengePollEvents(false); }, app.stonehengeViewModelName.model.StonehengePollDelay);
                    }
                })
                .catch(error => {
                    if (app.stonehengeViewModelName.model.StonehengePollEventsActive !== null) {
                        app.stonehengeViewModelName.model.StonehengeIsDisconnected = true;
                    }
                    if (error.responseType !== "abort") {
                        //debugger;
                        if (status === 200) {
                            setTimeout(function () { window.location.reload(); }, 1000);
                        }
                        app.stonehengeViewModelName.model.StonehengePollEventsActive = null;
                        if (!app.stonehengeViewModelName.model.StonehengePostActive) {
                            setTimeout(function () { app.stonehengeViewModelName.StonehengePollEvents(true); }, app.stonehengeViewModelName.model.StonehengePollDelay);
                        }
                    }
                });
        },

        StonehengeGetViewModel: function () {
            this.StonehengeCancelRequests();
            app.$http.get('ViewModel/stonehengeViewModelName')
                .then(response => {
                    var cookie = response.headers.get("cookie");
                    var match = (/stonehenge-id=([0-9a-fA-F]+)/).exec(cookie);
                    if (match == null) {
                        app.stonehengeViewModelName.model.StonehengeSession = stonehengeGetCookie("stonehenge-id");
                    }
                    else {
                        app.stonehengeViewModelName.model.StonehengeSession = match[1];
                    }
                    try {
                        let data = JSON.parse(response.bodyText);
                        app.stonehengeViewModelName.StonehengeSetViewModelData(data);
                    } catch (error) {
                        if (console && console.log) console.log(error);
                    }
                    if (app.stonehengeViewModelName.model.StonehengeInitialLoading) {
                        if (typeof (app.stonehengeViewModelName.user_InitialLoaded) == 'function') {
                            try {
                                app.stonehengeViewModelName.user_InitialLoaded(app.stonehengeViewModelName.model);
                            } catch (e) { }
                        }
                    } 
                    app.stonehengeViewModelName.model.StonehengeInitialLoading = false;
                    app.stonehengeViewModelName.model.StonehengeIsLoading = false;
                    if (app.stonehengeViewModelName.model.StonehengePollEventsActive === null) {
                        setTimeout(function () { app.stonehengeViewModelName.StonehengePollEvents(true); }, app.stonehengeViewModelName.model.StonehengePollDelay);
                    }
                })
                .catch(error => {
                    app.stonehengeViewModelName.model.StonehengeIsDisconnected = true;
                    debugger;
                    if (console && console.log) console.log(error);
                    setTimeout(function () { window.location.reload(); }, 1000);
                    window.location.reload();
                });

            console.log('vm loaded');
        },

        model: {

            StonehengeActive: false,
            StonehengePollEventsActive: null,
            StonehengePollDelay: 10000,
            StonehengeInitialLoading: true,
            StonehengeIsLoading: true,
            StonehengeIsDirty: false,
            StonehengeIsDisconnected: false,
            StonehengePostActive: false,
            StonehengeSession: '<none>'
            //stonehengeProperties

        },

        data: function () {
            console.log('get data');
            //debugger;
            app.stonehengeViewModelName.StonehengeGetViewModel();
            app.stonehengeViewModelName.model.StonehengeActive = true;

            return app.stonehengeViewModelName.model;
        },
        methods: {
            /*commands*/
        }
    };

    app.stonehengeViewModelName = vm;
    console.log('stonehengeViewModelName created');

    return vm;
};
