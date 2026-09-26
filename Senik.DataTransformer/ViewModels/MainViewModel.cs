using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Senik.DataTransformer.Core;
using System.Windows;
using System.Windows.Controls;


namespace Senik.DataTransformer.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // متغیر کنترل‌کننده نمایش دیالوگ
        [ObservableProperty]
        private bool isDialogVisible;

        // فرمانی برای باز کردن دیالوگ (از داخل DiscoveryPage صدا زده می‌شود)
        [RelayCommand]
        public void OpenDialog()
        {
            IsDialogVisible = true;
        }

        // فرمانی برای بستن دیالوگ
        [RelayCommand]
        public void CloseDialog()
        {
            IsDialogVisible = false;
        }

        // فرمانی برای شروع عملیات و رفتن به صفحه دوم
        [RelayCommand]
        public void StartExecution()
        {
            // ارسال پیام برای رفتن به صفحه دوم
            WeakReferenceMessenger.Default.Send(new NavigateToExecutionPageMessage());
        }
        [ObservableProperty]
        private string _connectedServerInfo = "در حال اتصال...";

        [ObservableProperty]
        private bool _isDatabaseConnected = false;
    }
}