﻿using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Battlehub.RTSaveLoad2
{
    


    public delegate void StorageEventHandler(Error error);
    public delegate void StorageEventHandler<T>(Error error, T data);

    public interface IStorage
    {
        void GetProject(string projectPath, StorageEventHandler<ProjectInfo> callback);
        void GetFolderTree(string projectPath, StorageEventHandler<ProjectItem> callback);
        void GetAssets(string projectPath, string[] folderPath, StorageEventHandler<ProjectItem[][]> callback);
    }

    public class FileSystemStorage : IStorage
    {
        private string RootPath
        {
            get { return Application.persistentDataPath; }
        }

        private string FullPath(string path)
        {
            return Path.Combine(RootPath, path);
        }

        private string AssetsFolderPath(string path)
        {
            return Path.Combine(Path.Combine(RootPath, path), "Assets");
        }

        public void GetProject(string projectPath, StorageEventHandler<ProjectInfo> callback)
        {
            projectPath = Path.Combine(FullPath(projectPath), "Project.rtmeta");
            ProjectInfo projectInfo;
            Error error = new Error();
            ISerializer serializer = RTSL2Deps.Get.Serializer;
            if (!File.Exists(projectPath))
            {
                projectInfo = new ProjectInfo();
            }
            else
            {
                try
                {
                    using (FileStream fs = File.OpenRead(projectPath))
                    {
                        projectInfo = serializer.Deserialize<ProjectInfo>(fs);
                    }       
                }
                catch (Exception e)
                {
                    projectInfo = new ProjectInfo();
                    error.ErrorCode = Error.E_Exception;
                    error.ErrorText = e.ToString();
                }
            }
            callback(error, projectInfo);
        }

        public void GetFolderTree(string projectPath, StorageEventHandler<ProjectItem> callback)
        {
            projectPath = AssetsFolderPath(projectPath);

            ProjectItem assets = new ProjectItem();
            assets.ItemID = 0;
            assets.Children = new List<ProjectItem>();
            assets.Name = "Assets";

            GetFolders(projectPath, assets);

            callback(new Error(), assets);
        }

        private static T GetItem<T>(ISerializer serializer, string path) where T : ProjectItem, new()
        {
            string metaFile = path + ".rtmeta";
            T item;
            if (File.Exists(metaFile))
            {
                try
                {
                    using (FileStream fs = File.OpenRead(metaFile))
                    {
                        item = serializer.Deserialize<T>(fs);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogErrorFormat("Unable to read meta file: {0} -> got exception: {1} ", metaFile, e.ToString());
                    item = new T();
                }
            }
            else
            {
                item = new T();
            }
            item.Name = Path.GetFileNameWithoutExtension(path);
            item.Ext = Path.GetExtension(path);
            return item;
        }

        private void GetFolders(string path, ProjectItem parent)
        {
            if(!Directory.Exists(path))
            {
                return;
            }

            ISerializer serializer = RTSL2Deps.Get.Serializer;
            string[] dirs = Directory.GetDirectories(path);
            for (int i = 0; i < dirs.Length; ++i)
            {
                string dir = dirs[i];
                ProjectItem projectItem = GetItem<ProjectItem>(serializer, dir);

                projectItem.Parent = parent;
                projectItem.Children = new List<ProjectItem>();
                parent.Children.Add(projectItem);

                GetFolders(dir, projectItem);
            }
        }

        public void GetAssets(string projectPath, string[] folderPath, StorageEventHandler<ProjectItem[][]> callback)
        {
            projectPath = AssetsFolderPath(projectPath);

            ISerializer serializer = RTSL2Deps.Get.Serializer;
            ProjectItem[][] result = new ProjectItem[folderPath.Length][];
            for (int i = 0; i < folderPath.Length; ++i)
            {
                string path = Path.Combine(projectPath, folderPath[i]);
                if (!Directory.Exists(path))
                {
                    continue;
                }

                string[] files = Directory.GetFiles(path);
                ProjectItem[] items = new ProjectItem[files.Length];
                for(int j = 0; j < files.Length; ++j)
                {
                    items[j] = GetItem<AssetItem>(serializer, files[j]);
                }

                result[i] = items;
            }

            callback(new Error(), result);
        }
    }
}