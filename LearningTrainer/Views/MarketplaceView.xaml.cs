using LearningTrainer.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace LearningTrainer.Views
{
    public partial class MarketplaceView : UserControl
    {
        private int _lastPageSize;

        public MarketplaceView()
        {
            InitializeComponent();
        }

        private void ContentArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is not MarketplaceViewModel vm) return;

            var width = e.NewSize.Width - MarketplaceViewModel.ContentPadding;
            var height = e.NewSize.Height - MarketplaceViewModel.ContentPadding;

            if (width <= 0 || height <= 0) return;

            var cols = Math.Max(1, (int)(width / MarketplaceViewModel.CardTotalWidth));
            var rows = Math.Max(1, (int)(height / MarketplaceViewModel.CardTotalHeight));
            var newPageSize = cols * rows;

            if (newPageSize != _lastPageSize)
            {
                _lastPageSize = newPageSize;
                vm.PageSize = newPageSize;
            }
        }
    }
}
