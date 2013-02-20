﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using TeamCityDesktop.Controls;
using TeamCityDesktop.Model;
using TeamCityDesktop.Windows;
using TeamCitySharp;
using Application = System.Windows.Application;

namespace TeamCityDesktop
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string ServerFile = "Servers.xml";
        private object activity;
        private ServerCredentialsModel serverCredentials;

        public MainWindow()
        {
            List<ServerCredentialsModel> credentials = null;
            if (File.Exists(ServerFile))
            {
                credentials = Serializer<List<ServerCredentialsModel>>.Load(ServerFile);
            }
            if (credentials == null || credentials.Count == 0)
            {
                // if no servers have been saved, create some default setting
                var newCredentials = new ServerCredentialsModel
                    {
                        Url = "teamcity.codebetter.com",
                        Guest = true
                    };
                var login = new Login(newCredentials);
                activity = login;
            }
            else
            {
                ShowServerOverview(credentials[0]);
            }

            InitializeComponent();
        }

        public object Activity
        {
            get { return activity; }
            set
            {
                if (activity is IDisposable)
                {
                    ((IDisposable)activity).Dispose();
                }
                activity = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Activity"));
                }
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        private void ShowServerOverview(ServerCredentialsModel credentials)
        {
            serverCredentials = credentials;
            RequestManager.Instance.Connect(credentials);

            // update cache right away
            RequestManager.Instance.GetProjectsAsync(null);
            RequestManager.Instance.GetBuildConfigsAsync(null);

            var overview = new ServerOverview();
            Activity = overview;
        }

        private void ConnectCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var login = activity as Login;
            e.CanExecute = login == null
                ? false
                : login.ServerCredentials.IsValid();
        }

        private void ConnectExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var login = activity as Login;
            if (login != null)
            {
                // save the credential info
                List<ServerCredentialsModel> credentials = File.Exists(ServerFile)
                    ? Serializer<List<ServerCredentialsModel>>.Load(ServerFile)
                    : new List<ServerCredentialsModel>();
                credentials.Add(login.ServerCredentials);
                Serializer<List<ServerCredentialsModel>>.Save(credentials, ServerFile);

                ShowServerOverview(login.ServerCredentials);
            }
        }

        private void DownloadArtifactsCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is IList<ArtifactModel> && ((IList)e.Parameter).Count > 0)
            {
                e.CanExecute = true;
            }
        }

        private void DownloadArtifactsExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TeamCityClient client = serverCredentials.CreateClient();
                var downloader = new ArtifactDownloader(client, dialog.SelectedPath, ((IList<ArtifactModel>)e.Parameter));
                downloader.RunWorkerAsync();
                new ProgressDialog(downloader)
                    {
                        Owner = Application.Current.MainWindow,
                        Title = "Downloading artifacts..."
                    }.ShowDialog();
            }
        }
    }
}
