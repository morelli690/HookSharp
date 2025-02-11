﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace HookSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"DllName\t\tOffset\t\tOriginal\tNew");

            string processName = "notepad++";

            ScanDll(processName, "ntdll.dll");
            ScanDll(processName, "kernel32.dll");
            ScanDll(processName, "user32.dll");

            Console.WriteLine($"");
            Console.Write($"Scan completed...");
            Console.ReadKey();
        }

        static void ScanDll (string remoteProcessName, string dllName)
        {
            byte[] bytesFromMyMemory = Process.GetCurrentProcess().GetByteFromProcessModule(dllName);
            byte[] bytesFromRemoteMemory = Process.GetProcessesByName(remoteProcessName).FirstOrDefault().GetByteFromProcessModule(dllName);

            // http://www.pinvoke.net/default.aspx/Structures.IMAGE_DOS_HEADER
            int e_lfanew = bytesFromMyMemory[0x3C];

            // https://docs.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-_image_optional_header
            int optionalHeaderOffset = 0x18;
            int sizeOfCodeOffset = e_lfanew + optionalHeaderOffset + 0x4;
            int BaseOfCodeOffset = e_lfanew + optionalHeaderOffset + 0x14;

            // https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-dtyp/262627d8-3418-4627-9218-4ffe110850b2
            uint BaseOfCode = BitConverter.ToUInt32(bytesFromMyMemory, BaseOfCodeOffset);
            uint sizeOfCode = BitConverter.ToUInt32(bytesFromMyMemory, sizeOfCodeOffset);

            for (uint i = BaseOfCode; i < sizeOfCode; i++)
            {
                byte original = bytesFromMyMemory[i];
                byte possiblyTampered = bytesFromRemoteMemory[i];

                if (original != possiblyTampered)
                {
                    Console.WriteLine($"{dllName}\t0x{i.ToString("X")}\t\t0x{original.ToString("X")}\t\t0x{possiblyTampered.ToString("X")}");
                }
            }
        }
    }

    public static class ProcessHelper
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        public static byte[] GetByteFromProcessModule (this Process process, string moduleName)
        {
            ProcessModule module = process.Modules.Cast<ProcessModule>().Where(x => x.ModuleName.ToUpper() == moduleName.ToUpper()).FirstOrDefault();

            int bytesRead = 0;

            byte[] buffer = new byte[module.ModuleMemorySize];

            IntPtr processHandle = OpenProcess(0x10 /* VirtualMemoryRead */, false, process.Id);

            ReadProcessMemory((int)processHandle, module.BaseAddress, buffer, buffer.Length, ref bytesRead);

            CloseHandle(processHandle);

            return buffer;
        }
    }
}
