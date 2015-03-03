using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
namespace TakeANote
{
    public partial class About : PhoneApplicationPage
    {
        public About()
        {
            InitializeComponent();
            copyright.Text = "Copyright © " + DateTime.Now.Year + " All rights reserved.";
        }

        private void btnContact_Click(object sender, RoutedEventArgs e)
        {
            EmailComposeTask emailComposeTask = new EmailComposeTask();

            emailComposeTask.Subject = "";
            emailComposeTask.Body = "";
            emailComposeTask.To = "hovokhc@outlook.com";

            emailComposeTask.Show();

        }

        private void btnRate_Click(object sender, RoutedEventArgs e)
        {
            MarketplaceReviewTask marketplaceReviewTask = new MarketplaceReviewTask();

            marketplaceReviewTask.Show();

        }

    }
}