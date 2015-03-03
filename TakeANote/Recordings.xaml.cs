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
using System.Diagnostics;
using TakeANote.ViewModels;
using System.IO.IsolatedStorage;
using System.IO;
using System.Windows.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework.GamerServices;
using System.Windows.Media;

namespace TakeANote
{
    public partial class Recordings : PhoneApplicationPage
    {
        private int lastSelectedIndex = -1;
        private Brush lastSelectedItemBackground;
        private ItemViewModel lastSelectedItem = null;
        Microphone microphone = Microphone.Default;

        byte[] buffer;

        MemoryStream stream;
        SoundEffectInstance sound = null;
        bool inRecordingMode = false;
        bool playbackStarted = false;

        public Recordings()
        {
            InitializeComponent();
            App.ViewModel.LoadData();
            DataContext = App.ViewModel;
            this.Loaded += new RoutedEventHandler(Page_Loaded);


            // Timer to simulate the XNA Game Studio game loop (Microphone is from XNA Game Studio)
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(33);
            dt.Tick += delegate
            {
                try
                {
                    FrameworkDispatcher.Update();

                    //
                    // We not only want to run the "game loop"
                    // We want to update our UX based on the state of playback
                    //
                    if (sound != null)
                    {
                        //
                        // We can't know playback has actually started until we enter our
                        // timer code and the flag is false but the player is playing
                        //
                        if (!playbackStarted && sound.State == SoundState.Playing)
                        {
                            playbackStarted = true;
                        }
                        else if (playbackStarted && sound.State == SoundState.Stopped)
                        {
                            //
                            // When we've detected playback and the sound is stopped
                            // we want to change the text of the button back to 'play'
                            // and revert to the play icon
                            //
                            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).Text = "play";
                            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IconUri = new Uri("/Images/appbar.transport.play.rest.png", UriKind.Relative);
                            sound = null;
                            playbackStarted = false;
                        }
                    }
                }
                catch { }
            };
            dt.Start();

            // Required for XNA Microphone API to work
            Microsoft.Xna.Framework.FrameworkDispatcher.Update();

        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            lastSelectedIndex = -1;
            lastSelectedItem = null;
        }

        private void RecordingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If selected index is -1 (no selection) do nothing
            if (MainListBox.SelectedIndex == -1)
                return;

            if (lastSelectedIndex != -1)
            {
                var listBox = sender as ListBox;
                var listBoxItem = listBox.ItemContainerGenerator.ContainerFromIndex(lastSelectedIndex) as ListBoxItem;

                listBoxItem.Background = lastSelectedItemBackground;
            }

            ItemViewModel ivm = (ItemViewModel)MainListBox.SelectedItem;

            if (ivm != null)
            {
                Debug.WriteLine("Selection: " + ivm.DisplayString + " / " + ivm.FileName);

                var listBox = sender as ListBox;
                var listBoxItem = listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) as ListBoxItem;

                lastSelectedItemBackground = listBoxItem.Background;
                lastSelectedIndex = MainListBox.SelectedIndex;
                System.Windows.Media.Color currentAccentColorHex = (System.Windows.Media.Color)Application.Current.Resources["PhoneAccentColor"];
                listBoxItem.Background = new SolidColorBrush(currentAccentColorHex);
                lastSelectedItem = ivm;
            }
        }

        private void PlaySelected_ClickHandler(object sender, EventArgs e)
        {
            if (lastSelectedItem == null)
            {
                MessageBox.Show("Please select a recording");
                return;
            }
            ApplicationBarIconButton btn = (ApplicationBarIconButton)ApplicationBar.Buttons[0];

            if (btn.Text == "play")
            {
                btn.Text = "pause";
                btn.IconUri = new Uri("/Images/appbar.transport.pause.rest.png", UriKind.Relative);
                if (sound != null)
                {
                    sound.Resume();
                    return;
                }
            }
            else if (btn.Text == "pause")
            {
                sound.Pause();
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).Text = "play";
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IconUri = new Uri("/Images/appbar.transport.play.rest.png", UriKind.Relative);
                return;
            }

            //
            // To play it, we first have to load it into memory
            //
            var isoStore = IsolatedStorageFile.GetUserStoreForApplication();
            IsolatedStorageFileStream fstream = isoStore.OpenFile(lastSelectedItem.FileName, System.IO.FileMode.Open);

            stream = new MemoryStream();

            buffer = new byte[fstream.Length];
            int actualSize = fstream.Read(buffer, 0, (int) fstream.Length);
            if (actualSize > 0)
            {
                stream.Write(buffer, 0, actualSize);
            }
            fstream.Close();


            sound = new SoundEffect(stream.ToArray(), microphone.SampleRate, AudioChannels.Mono).CreateInstance();
            sound.Play();

        }

        private void DeleteSelected_ClickHandler(object sender, EventArgs e)
        {
            if (lastSelectedItem == null)
            {
                MessageBox.Show("No recording selected");
                return;
            }

            Guide.BeginShowMessageBox("Please Confirm", "Are you sure you want to delete the selected recording?", new string[] { "Yes", "NO" }, 0, MessageBoxIcon.Warning, new AsyncCallback(OnMessageBoxAction), null);

        }

        private void DeletePart2()
        {
            string infoFileName = lastSelectedItem.FileName.Replace(".dat", ".info");
            var isoStore = IsolatedStorageFile.GetUserStoreForApplication();
            isoStore.DeleteFile(lastSelectedItem.FileName);
            isoStore.DeleteFile(infoFileName);
            App.ViewModel.LoadData();
            DataContext = App.ViewModel;
            App.ViewModel.FilesUpdated();
            // The following line was added to prevent the confirmation dialog from appearing when no recording is selected
            lastSelectedItem = null;
        }


        private void OnMessageBoxAction(IAsyncResult ar)
        {
            int? selectedButton = Guide.EndShowMessageBox(ar);
            switch (selectedButton)
            {
                case 0:
                    Deployment.Current.Dispatcher.BeginInvoke(() => DeletePart2());
                    break;

                case 1:               
                    break;

                default:
                    break;
            }
        }


    }
}