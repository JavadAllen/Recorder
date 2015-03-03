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
using System.IO;
using System.Windows.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Diagnostics;
using Microsoft.Phone.Shell;
using System.IO.IsolatedStorage;

namespace TakeANote
{
    public partial class MainPage : PhoneApplicationPage
    {
        Microphone microphone = Microphone.Default;
        byte[] buffer;
        MemoryStream stream;
        SoundEffectInstance sound = null;
        bool inRecordingMode = false;
        bool playbackStarted = false;
        string lastRecordedFileName = null;

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(MainPage_Loaded);

            //
            // In order to use code from the XNA framework, we need to
            // "poll" by calling FrameworkDispatcher.Update
            //
            // In this app, we'll use a 
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(15);
            dt.Tick += delegate {
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
                            Debug.WriteLine("Playback ended");
                            ((ApplicationBarIconButton)ApplicationBar.Buttons[1]).Text = "play";
                            ((ApplicationBarIconButton)ApplicationBar.Buttons[1]).IconUri = new Uri("/Images/appbar.transport.play.rest.png", UriKind.Relative);
                            this.TimerTextBlock.Text = "";
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

            //
            // In order for our "meter" to operate in a responsive way
            // we ask for updates every 100ms.
            //
            Microphone.Default.BufferDuration = TimeSpan.FromSeconds(.1);

            microphone.BufferReady += new EventHandler<EventArgs>(microphone_BufferReady);

            //
            // This allow us to record under the lock screen
            //
            PhoneApplicationService.Current.ApplicationIdleDetectionMode =IdleDetectionMode.Disabled;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            TimerTextBlock.Text = "Tap record to begin";

        }

        
        /// <summary>
        /// Callback method for microphone data.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void microphone_BufferReady(object sender, EventArgs e)
        {
            int size = microphone.GetData(buffer);
            if (size == 0)
                return;

            //
            // Write the buffer to the stream
            //
            stream.Write(buffer, 0, buffer.Length);

            //
            // Then compute the average amplitude so we can update
            // the "meter"
            //
            long aggregate = 0;
            int sampleIndex = 0;

            while (sampleIndex < size)
            {
                //
                // We need to extract a 2 byte (16-bit) sample from the buffer and
                // compute its absolute value because while samples are in the range
                // of -32768..32767 only the amplitude is important
                //
                short sample = BitConverter.ToInt16(this.buffer, sampleIndex);
                //
                // upscale it to a 32-bit value as Math.Abs expects an int
                //
                int sampleAsInt = (int)sample;
                int value = Math.Abs(sampleAsInt);

                aggregate += value;

                sampleIndex += 2;
            }

            int volume =  (int)(aggregate / (size / 2));


            //
            // Now, translate the "volume" number into the number of pixels
            // of our MeterBar.jpg image which should be visible
            //
            long meterCoverSize = volume / 10;

            //
            // And make sure it doesn't exceed 420 (the size of our bar)
            // 
            if (meterCoverSize > 420)
                meterCoverSize = 420;
            Debug.WriteLine("meterCoverSize: " + meterCoverSize.ToString());
            //
            // Then update the MeterCover UX object's size
            //
            Thickness newMargin = MeterCover.Margin;

            newMargin.Left =  21 +  meterCoverSize;
            MeterCover.Margin = newMargin;
            MeterCover.Width = 420 - meterCoverSize;


            TimeSpan recordingLength = Microphone.Default.GetSampleDuration(
                                     (int)this.stream.Position);
            this.TimerTextBlock.Text = String.Format("{0:00}:{1:00}",
              recordingLength.Minutes, recordingLength.Seconds);
        }


        /// <summary>
        /// Handle tap on the record/stop button.
        /// If recording, stop, if not recording, start it up.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void recordButton_Click(object sender, EventArgs e)
        {
            if (inRecordingMode)
            {
                microphone.Stop();
                Thickness newMargin = MeterCover.Margin;

                newMargin.Left = 21;
                MeterCover.Margin = newMargin;
                MeterCover.Width = 420;
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).Text = "record";
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IconUri = new Uri("/Images/appbar.microphone.png", UriKind.Relative);

                SaveRecording();

                this.TimerTextBlock.Text = "Recording Complete";
                inRecordingMode = false;
            }
            else
            {
                stream = new MemoryStream();
                buffer = new byte[microphone.GetSampleSizeInBytes(microphone.BufferDuration)];
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).Text = "stop";
                ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IconUri = new Uri("/Images/appbar.stop.rest.png", UriKind.Relative);

                microphone.Start();
                inRecordingMode = true;
            }
        }

        /// <summary>
        /// Handle tap on the play/pause button.
        /// If recording, ignore it
        /// If playing, pause
        /// If paused, resume
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void playButton_Click(object sender, EventArgs e)
        {
            if (inRecordingMode)
                return;

            if (lastRecordedFileName == null)
            {
                MessageBox.Show("No Sound Has Been Recorded Yet");
                return;
            }
            ApplicationBarIconButton btn = (ApplicationBarIconButton)ApplicationBar.Buttons[1];

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
                ((ApplicationBarIconButton)ApplicationBar.Buttons[1]).Text = "play";  
                ((ApplicationBarIconButton)ApplicationBar.Buttons[1]).IconUri = new Uri("/Images/appbar.transport.play.rest.png", UriKind.Relative);
                this.TimerTextBlock.Text = "Paused";
                return;
            }

            this.TimerTextBlock.Text = "Playing";

            //
            // To play it, we first have to load it into memory
            //
            var isoStore = IsolatedStorageFile.GetUserStoreForApplication();
            IsolatedStorageFileStream fstream = isoStore.OpenFile(lastRecordedFileName, System.IO.FileMode.Open);

            stream = new MemoryStream();

            buffer = new byte[fstream.Length];
            int actualSize = fstream.Read(buffer, 0, (int)fstream.Length);
            if (actualSize > 0)
            {
                stream.Write(buffer, 0, actualSize);
            }
            fstream.Close();

            sound = new SoundEffect(stream.ToArray(), microphone.SampleRate, AudioChannels.Mono).CreateInstance();
            sound.Play();
        }

        /// <summary>
        /// Handle tap on the save button.
        /// Write the contents of our stream to a file in IsolatedStorage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveRecording()
        {

            var isoStore = System.IO.IsolatedStorage.IsolatedStorageFile.GetUserStoreForApplication();
            DateTime fileTime = DateTime.Now;
            long fileTicks = fileTime.Ticks;
            string datFileName = fileTicks.ToString() + ".dat";
            lastRecordedFileName = datFileName;

            using (var targetFile = isoStore.CreateFile(datFileName))
            {
                var dataBuffer = stream.GetBuffer();
                targetFile.Write(dataBuffer, 0, (int)stream.Length);
                targetFile.Flush();
                targetFile.Close();
                stream = null;
            }

            using (var targetFile = isoStore.CreateFile(fileTicks.ToString() + ".info"))
            {
                StreamWriter sw = new StreamWriter(targetFile);
                sw.WriteLine(datFileName + "|" + fileTime.ToString());
                sw.Flush();
                sw.Close();
                stream = null;
            }

        }

        /// <summary>
        /// Handle tap on the folders button
        /// Navigate to the page which lists our files.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void listButton_Click(object sender, EventArgs e)
        {
            if (inRecordingMode)
                return;

            // Navigate to the new page
            NavigationService.Navigate(new Uri("/Recordings.xaml", UriKind.Relative));
            TimerTextBlock.Text = "";
        }

        private void About_Click(object sender, EventArgs e)
        {
            if (inRecordingMode)
                return;

            // Navigate to the new page
            NavigationService.Navigate(new Uri("/About.xaml", UriKind.Relative));
            TimerTextBlock.Text = "";
        }

    }
}