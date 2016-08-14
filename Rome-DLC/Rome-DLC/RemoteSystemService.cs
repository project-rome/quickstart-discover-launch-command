using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.System.RemoteSystems;

namespace Rome_DLC
{
    internal class RemoteSystemService
    {
        private static BackgroundTaskDeferral _serviceDeferral;
        public static event EventHandler<string> OnStatusUpdateMessage;
        public static bool KeepConnectionOpen { get; set; } = true;
        private static int _connectionRetryCount = 5;
        private static AppServiceConnection _appServiceConnection;

        public static async Task LaunchAndConnect(RemoteSystem remoteSystem)
        {
            //Launch app on remote device
            var uri = new Uri("rome-dlc:");
            var launchUriStatus = await RemoteLauncher.LaunchUriAsync(new RemoteSystemConnectionRequest(remoteSystem), uri);

            if (launchUriStatus == RemoteLaunchUriStatus.Success)
            {
                StatusWrite("App launched on: " + remoteSystem.DisplayName);

                await SendMessageToRemoteSystemAsync(remoteSystem, "Hello");
            }
            else
            {
                StatusWrite("Error: " + launchUriStatus);
            }
        }
        public static async Task SendMessageToRemoteSystemAsync(RemoteSystem remoteSystem, string messageString)
        {

            if (await OpenAppServiceConnectionAsync(remoteSystem) == AppServiceConnectionStatus.Success)
            {
                var inputs = new ValueSet { ["message"] = messageString };
                var response = await _appServiceConnection.SendMessageAsync(inputs);
                if (response.Status == AppServiceResponseStatus.Success)
                {
                    if (response.Message.ContainsKey("result"))
                    {
                        var resultText = response.Message["result"].ToString();

                        StatusWrite("Sent message: \"" + messageString + "\" to device: " + remoteSystem.DisplayName +
                                    " response: " + resultText);
                    }
                }
                else
                {
                    StatusWrite("Error: " + response.Status);
                }

                if (KeepConnectionOpen == false)
                {
                    CloseAppServiceConnection();
                }
            }
            
        }

        private static void CloseAppServiceConnection()
        {
            if (_appServiceConnection != null)
            {
                _appServiceConnection.Dispose();
                _appServiceConnection = null;
            }
        }

        private static async Task<AppServiceConnectionStatus> OpenAppServiceConnectionAsync(RemoteSystem remoteSystem)
        {
            //Connect App Service
            var connectionRequest = new RemoteSystemConnectionRequest(remoteSystem);

            if (_appServiceConnection == null)
            {
                _appServiceConnection = new AppServiceConnection()
                {
                    AppServiceName = "com.project-rome.echo",
                    PackageFamilyName = Package.Current.Id.FamilyName
                };

                _appServiceConnection.ServiceClosed += async (sender, args) =>
                {
                    StatusWrite("AppServiceConnection closed to: " + remoteSystem.DisplayName + ", reason: " + args.Status);

                    if (--_connectionRetryCount > 0)
                    {
                        await OpenAppServiceConnectionAsync(remoteSystem);
                    }
                };

                var status = await _appServiceConnection.OpenRemoteAsync(connectionRequest);

                if (status != AppServiceConnectionStatus.Success)
                {
                    StatusWrite("Error: " + status);
                    _appServiceConnection = null;
                }

                StatusWrite("AppServiceConnection opened to: " + remoteSystem.DisplayName);
                _connectionRetryCount = 5;

                return status;
            }

            return AppServiceConnectionStatus.Success;
        }

        public static void OnMessageReceived(BackgroundActivatedEventArgs args)
        {
            var taskInstance = args.TaskInstance;
            var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

            if (details != null && details.Name == "com.project-rome.echo")
            {
                //Take a service deferral so the service isn't terminated
                _serviceDeferral = taskInstance.GetDeferral();
                taskInstance.Canceled += (sender, reason) => _serviceDeferral.Complete();


                //Listen for incoming app service requests
                details.AppServiceConnection.RequestReceived += async (sender, eventArgs) =>
                {
                    var valueSet = eventArgs.Request.Message;
                    var messageString = valueSet["message"] as string;
                    try
                    {
                        var result = new ValueSet { ["result"] = "OK" };

                        //Send the response
                        await eventArgs.Request.SendResponseAsync(result);
                        StatusWrite("Received message: \"" + messageString + "\"");
                    }
                    finally
                    {
                        _serviceDeferral.Complete();
                    }
                    _serviceDeferral.Complete();

                };
            }
        }

        private static void StatusWrite(string v)
        {
            Debug.WriteLine(v);
            OnStatusUpdateMessage?.Invoke(null, v);
        }
    }
}