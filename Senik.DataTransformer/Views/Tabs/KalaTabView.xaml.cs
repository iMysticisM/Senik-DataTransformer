using Senik.DataTransformer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Senik.DataTransformer.Views.Tabs
{
    /// <summary>
    /// Interaction logic for KalaTabView.xaml
    /// </summary>
    public partial class KalaTabView : UserControl
    {
        public KalaTabView()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                // ثبت دکمه‌ها برای راهنما
                SenikTourGuide.Register("SelectFile", SelectFile);
                SenikTourGuide.Register("Download", SampleDownload);
                SenikTourGuide.Register("Mapping", BtnMapping);
            };
        }
    }   
}
