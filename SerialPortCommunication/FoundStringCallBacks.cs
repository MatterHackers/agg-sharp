using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace MatterHackers.SerialPortConnecton
{
    public class FoundStringEventArgs : EventArgs
    {
        public bool CallbackWasCalled { get; set; }
        bool sendToDelegateFunctions = true;
        string lineToCheck;

        public FoundStringEventArgs(string lineReceived)
        {
            this.lineToCheck = lineReceived;
        }

        public string LineToCheck { get { return lineToCheck; } }

        public bool SendToDelegateFunctions
        {
            get
            {
                return sendToDelegateFunctions;
            }
            set
            {
                sendToDelegateFunctions = value;
            }
        }
    }

    public class FoundStringCallBacks
    {
        public delegate void FoundStringEventHandler(object sender, EventArgs foundStringEventArgs);

        public Dictionary<string, FoundStringEventHandler> dictionaryOfCallBacks = new Dictionary<string, FoundStringEventHandler>();

        public void AddCallBackToKey(string key, FoundStringEventHandler value)
        {
            if (dictionaryOfCallBacks.ContainsKey(key))
            {
                dictionaryOfCallBacks[key] += value;
            }
            else
            {
                dictionaryOfCallBacks.Add(key, value);
            }
        }

        public void RemoveCallBackFromKey(string key, FoundStringEventHandler value)
        {
            if (dictionaryOfCallBacks.ContainsKey(key))
            {
                if (dictionaryOfCallBacks[key] == null)
                {
                    throw new Exception();
                }
                dictionaryOfCallBacks[key] -= value;
                if (dictionaryOfCallBacks[key] == null)
                {
                    dictionaryOfCallBacks.Remove(key);
                }
            }
            else
            {
                throw new Exception();
            }
        }
    }

    public class FoundStringStartsWithCallbacks : FoundStringCallBacks
    {

        public void CheckForKeys(EventArgs e)
        {
            foreach (KeyValuePair<string, FoundStringEventHandler> pair in this.dictionaryOfCallBacks)
            {
                FoundStringEventArgs foundString = e as FoundStringEventArgs;
                if (foundString != null && foundString.LineToCheck.StartsWith(pair.Key))
                {
                    foundString.CallbackWasCalled = true;
                    pair.Value(this, e);
                }
            }
        }
    }

    public class FoundStringContainsCallbacks : FoundStringCallBacks
    {

        public void CheckForKeys(EventArgs e)
        {
            foreach (KeyValuePair<string, FoundStringEventHandler> pair in this.dictionaryOfCallBacks)
            {
                FoundStringEventArgs foundString = e as FoundStringEventArgs;
                if (foundString != null && foundString.LineToCheck.Contains(pair.Key))
                {
                    foundString.CallbackWasCalled = true;
                    pair.Value(this, e);
                }
            }
        }
    }
}