﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EasyBackup.Helpers
{
    class Utilities
    {
        // Calculating free space of drive: https://stackoverflow.com/a/13578940/3938401
        // Pinvoke for API function
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

        public static ulong DriveFreeBytes(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return 0;
            }

            if (!folderName.EndsWith("\\"))
            {
                folderName += '\\';
            }

            ulong freeBytesAvailable = 0;
            ulong totalNumberOfBytes = 0;
            ulong totalNumberOfFreeBytes = 0;

            if (GetDiskFreeSpaceEx(folderName, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes))
            {
                return freeBytesAvailable;
            }
            return 0;

        }
    }
}
