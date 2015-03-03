using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.IO.IsolatedStorage;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace TakeANote.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            this.Items = new ObservableCollection<ItemViewModel>();
        }

        /// <summary>
        /// A collection for ItemViewModel objects.
        /// </summary>
        public ObservableCollection<ItemViewModel> Items { get; private set; }


        /// <summary>
        /// Creates and adds a few ItemViewModel objects into the Items collection.
        /// </summary>
        public void LoadData()
        {
            this.Items = new ObservableCollection<ItemViewModel>();
            var isoStore = IsolatedStorageFile.GetUserStoreForApplication();
            string[] fileNames = isoStore.GetFileNames();
            List<RecordingInfo> itemValues = new List<RecordingInfo>();

            foreach (string fileName in fileNames)
            {
                if (fileName.EndsWith(".info"))
                {
                    IsolatedStorageFileStream f = isoStore.OpenFile(fileName, System.IO.FileMode.Open);
                    StreamReader sr = new StreamReader(f);
                    string data = sr.ReadLine();
                    sr.Close();
                    f.Close();
                    string[] dataParts = data.Split('|');
                    Debug.WriteLine("File Name: " + dataParts[0] + " - date string: " + dataParts[1]);
                    itemValues.Add(new RecordingInfo
                    {
                        displayString = dataParts[1],
                        fileName = dataParts[0]
                    });
                }
            }

            if (itemValues.Count == 0)
            {
                ItemViewModel ivm = new ItemViewModel();
                ivm.FileName = "No Files Have Been Recorded";
                this.Items.Add(ivm);
            }
            else
            {
                var descList = from s in itemValues
                           orderby s.displayString descending
                           select s;

                foreach (RecordingInfo s in descList)
                {
                    ItemViewModel ivm = new ItemViewModel();
                    ivm.DisplayString = s.displayString;
                    ivm.FileName = s.fileName;
                    this.Items.Add(ivm);
                }
            }
        }

        public void FilesUpdated()
        {
            NotifyPropertyChanged("Items");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}