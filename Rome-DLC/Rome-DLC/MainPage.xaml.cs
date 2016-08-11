using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System.RemoteSystems;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Rome_DLC
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MainPage
    {
        public ObservableCollection<RemoteSystem> DeviceList { get; } = new ObservableCollection<RemoteSystem>();
        public ObservableCollection<string> StatusMessageCollection { get;  } = new ObservableCollection<string>();

        private RemoteSystem _selectedRemoteSystem;
        private RemoteSystemWatcher _remoteSystemWatcher;

        public MainPage()
        {
            InitializeComponent();
            DataContext = this;

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            RemoteSystemService.OnStatusUpdateMessage += async (sender, s) => 
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => StatusMessageCollection.Add(s));

            DiscoverDevices();
        }

        private async void DeviceListComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRemoteSystem = DeviceListComboBox.SelectedItem as RemoteSystem;

            if (_selectedRemoteSystem != null)
            {
                await RemoteSystemService.LaunchAndConnect(_selectedRemoteSystem);
            }
        }

        private async void DiscoverDevices()
        {
            var accessStatus = await RemoteSystem.RequestAccessAsync();
            if (accessStatus == RemoteSystemAccessStatus.Allowed)
            {
                _remoteSystemWatcher = RemoteSystem.CreateWatcher();

                //Add RemoteSystem to DeviceList (on the UI Thread)
                _remoteSystemWatcher.RemoteSystemAdded += async (sender, args) =>
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => DeviceList.Add(args.RemoteSystem));

                //Remove RemoteSystem from DeviceList (on the UI Thread)
                _remoteSystemWatcher.RemoteSystemRemoved += async (sender, args) =>
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => DeviceList.Remove(DeviceList.FirstOrDefault(system => system.Id == args.RemoteSystemId)));

                //Update RemoteSystem on DeviceList (on the UI Thread)
                _remoteSystemWatcher.RemoteSystemUpdated += async (sender, args) =>
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DeviceList.Remove(DeviceList.FirstOrDefault(system => system.Id == args.RemoteSystem.Id));
                        DeviceList.Add(args.RemoteSystem);
                    });

                _remoteSystemWatcher.Start();
            }
        }


        private async void Send_OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectedRemoteSystem != null)
            {
                await RemoteSystemService.SendMessageToRemoteSystemAsync(_selectedRemoteSystem, MessageTextBox.Text);
            }
             
        }
    }
}
