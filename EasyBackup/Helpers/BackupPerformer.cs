﻿using EasyBackup.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyBackup.Helpers
{
    // TODO: add option to not always overwrite and use an existing backup (essentially adding new/updated files)
    // TODO: fix possible recursion issues with symlinks :O
    class BackupPerformer
    {
        // // // Events and Delegates // // //

        public delegate void StartedFinishedCopyingItemHandler(FolderFileItem item);
        public event StartedFinishedCopyingItemHandler StartedCopyingItem;
        public event StartedFinishedCopyingItemHandler FinishedCopyingItem;

        public delegate void CopiedBytesOfItemHandler(FolderFileItem item, ulong bytes);
        public event CopiedBytesOfItemHandler CopiedBytesOfItem;

        public delegate void BackupFailedHandler(Exception e);
        public event BackupFailedHandler BackupFailed;

        public delegate void CalculatedBytesOfItemHandler(FolderFileItem item, ulong bytes);
        public event CopiedBytesOfItemHandler CalculatedBytesOfItem;

        // // // // // // // // // // // // //

        public bool IsRunning { get; set; }
        public bool HasBeenCanceled { get; set; }
        private bool IsCalculatingFileSize { get; set; }
        private ulong CurrentDirectorySize { get; set; }

        public BackupPerformer()
        {
            IsRunning = false;
            HasBeenCanceled = false;
            IsCalculatingFileSize = false;
            CurrentDirectorySize = 0;
        }

        public void Cancel()
        {
            HasBeenCanceled = true;
        }

        public async Task CancelAsync()
        {
            HasBeenCanceled = true;
            while (IsRunning)
            {
                await Task.Delay(100); // wait for cancel to finish
            }
        }

        // Modified from https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private void CopyDirectory(FolderFileItem itemBeingCopied, string sourceDirName, string destDirName, bool copySubDirs)
        {
            if (HasBeenCanceled)
            {
                return;
            }
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                if (HasBeenCanceled)
                {
                    return;
                }
                if (IsCalculatingFileSize)
                {
                    CurrentDirectorySize += (ulong)file.Length;
                }
                else
                {
                    string tempPath = Path.Combine(destDirName, file.Name);
                    CopySingleFile(itemBeingCopied, file.FullName, tempPath);
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    if (HasBeenCanceled)
                    {
                        return;
                    }
                    string temppath = Path.Combine(destDirName, subDir.Name);
                    CopyDirectory(itemBeingCopied, subDir.FullName, temppath, copySubDirs);
                }
            }
        }

        // Modified from https://stackoverflow.com/a/33726939/3938401
        private void CopySingleFile(FolderFileItem itemBeingCopied, string source, string destination)
        {
            if (File.Exists(source))
            {
                if (HasBeenCanceled)
                {
                    return;
                }
                using (var outStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    using (var inStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int buffer_size = 10240;
                        byte[] buffer = new byte[buffer_size];
                        long total_read = 0;
                        while (total_read < inStream.Length)
                        {
                            if (HasBeenCanceled)
                            {
                                break;
                            }
                            int read = inStream.Read(buffer, 0, buffer_size);
                            outStream.Write(buffer, 0, read);
                            CopiedBytesOfItem(itemBeingCopied, (ulong)read);
                            total_read += read;
                        }
                    }
                }
            }
        }

        private void IterateOverFiles(List<FolderFileItem> paths, string backupDirectory)
        {
            try
            {
                IsRunning = true;
                if (Directory.Exists(backupDirectory))
                {
                    backupDirectory = Path.Combine(backupDirectory, "easy-backup", "backup-" + DateTime.Now.ToString("yyyy-MM-dd-H-mm-ss"));
                    if (!Directory.Exists(backupDirectory))
                    {
                        Directory.CreateDirectory(backupDirectory);
                    }
                    // ok, start copying the files!
                    foreach (FolderFileItem item in paths)
                    {
                        if (HasBeenCanceled)
                        {
                            break;
                        }
                        var directoryName = Path.GetDirectoryName(item.Path);
                        var pathRoot = Path.GetPathRoot(item.Path);
                        directoryName = directoryName.Replace(pathRoot, "");
                        directoryName = Path.Combine(pathRoot.Replace(":\\", ""), directoryName);
                        var outputDirectoryPath = Path.Combine(backupDirectory, directoryName);
                        if (!Directory.Exists(outputDirectoryPath))
                        {
                            Directory.CreateDirectory(outputDirectoryPath);
                        }
                        StartedCopyingItem?.Invoke(item);
                        if (item.IsDirectory)
                        {
                            if (Directory.Exists(item.Path))
                            {
                                if (item.OnlyCopiesLatestFile && item.CanEnableOnlyCopiesLatestFile)
                                {
                                    // scan directory and copy only the latest file out of it
                                    var directoryInfo = new DirectoryInfo(item.Path);
                                    var latestFile = directoryInfo.GetFiles().OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault();
                                    if (latestFile != null)
                                    {
                                        if (IsCalculatingFileSize)
                                        {
                                            CalculatedBytesOfItem?.Invoke(item, (ulong)new FileInfo(latestFile.FullName).Length);
                                        }
                                        else
                                        {
                                            var outputBackupDirectory = Path.Combine(outputDirectoryPath, Path.GetFileName(item.Path));
                                            // create directory if needed in backup path
                                            if (!Directory.Exists(outputBackupDirectory))
                                            {
                                                Directory.CreateDirectory(outputBackupDirectory);
                                            }
                                            var outputPath = Path.Combine(outputBackupDirectory, Path.GetFileName(latestFile.FullName));
                                            if (HasBeenCanceled)
                                            {
                                                break;
                                            }
                                            CopySingleFile(item, latestFile.FullName, outputPath);
                                        }
                                    }
                                }
                                else
                                {
                                    var outputPath = Path.Combine(outputDirectoryPath, Path.GetFileName(item.Path));
                                    if (HasBeenCanceled)
                                    {
                                        break;
                                    }
                                    CurrentDirectorySize = 0;
                                    CopyDirectory(item, item.Path, outputPath, item.IsRecursive);
                                    if (IsCalculatingFileSize)
                                    {
                                        CalculatedBytesOfItem?.Invoke(item, CurrentDirectorySize);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (IsCalculatingFileSize)
                            {
                                CalculatedBytesOfItem?.Invoke(item, (ulong)new FileInfo(item.Path).Length);
                            }
                            else
                            {
                                var outputPath = Path.Combine(outputDirectoryPath, Path.GetFileName(item.Path));
                                CopySingleFile(item, item.Path, outputPath);
                            }
                        }
                        if (!HasBeenCanceled)
                        {
                            FinishedCopyingItem?.Invoke(item);
                        }
                    }
                }
                else
                {
                    throw new Exception("Backup directory doesn't exist");
                }
                IsRunning = false;
            }
            catch (Exception e)
            {
                BackupFailed?.Invoke(e);
            }
            finally
            {
                IsRunning = false;
            }
        }

        public void CalculateBackupSize(List<FolderFileItem> paths, string backupDirectory)
        {
            IsCalculatingFileSize = true;
            IterateOverFiles(paths, backupDirectory);
            IsCalculatingFileSize = false;
        }

        public void PerformBackup(List<FolderFileItem> paths, string backupDirectory)
        {
            IterateOverFiles(paths, backupDirectory);
        }
    }
}
