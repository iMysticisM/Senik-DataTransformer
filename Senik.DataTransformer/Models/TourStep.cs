using System.Windows;

namespace Senik.DataTransformer.Models
{
    public class TourStep
    {
        public FrameworkElement TargetElement { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }

        public TourStep(FrameworkElement target, string title, string message)
        {
            TargetElement = target;
            Title = title;
            Message = message;
        }
    }
}