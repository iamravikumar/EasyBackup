﻿using EasyBackup.Helpers;
using EasyBackup.Interfaces;
using EasyBackup.Models;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EasyBackup.ViewModels
{
    class HomeScreenViewModel : BaseViewModel, IDropTarget
    {
        private ObservableCollection<FolderFileItem> _items;

        public HomeScreenViewModel(IChangeViewModel viewModelChanger) : base(viewModelChanger)
        {
            Items = new ObservableCollection<FolderFileItem>();
        }

        public ObservableCollection<FolderFileItem> Items
        {
            get { return _items; }
            set { _items = value; NotifyPropertyChanged(); }
        }

        public ICommand AddFolder
        {
            get { return new RelayCommand(ChooseFolder); }
        }

        private void ChooseFolder()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                Items.Add(new FolderFileItem() { Path = dialog.SelectedPath, IsRecursive = true });
            }
        }

        public ICommand AddFile
        {
            get { return new RelayCommand(ChooseFile); }
        }

        private void ChooseFile()
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            dialog.Multiselect = true;
            dialog.ShowReadOnly = true;
            dialog.Title = "Choose a file";
            if (dialog.ShowDialog(Application.Current.MainWindow).GetValueOrDefault())
            {
                foreach (string fileName in dialog.FileNames)
                {
                    Items.Add(new FolderFileItem() { Path = fileName });
                }
            }
        }

        #region IDropTarget

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject && (dropInfo.Data as DataObject).GetFileDropList().Count > 0)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            if (dropInfo.Data is DataObject)
            {
                var stringCollection = (dropInfo.Data as DataObject).GetFileDropList();
                foreach (string path in stringCollection)
                {
                    Items.Add(new FolderFileItem() { Path = path, IsDirectory = Directory.Exists(path) });
                }
            }
        }

        #endregion
    }
}
