using Android.App;
using Android.Bluetooth;
using Android.Content;
using Android.Nfc;
using Android.OS;
using Android.Widget;
//using Java.Lang;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using static Android.Content.IntentFilter;

namespace com.touchstar.chrisd.NFCPairLib
{
    [Activity(Label = "NFC Pair Library", Name = "NFCPairLib.NFCPairLib")]
    public static class NFCPairLib
    {
        public static readonly string ARG_REQUEST_CODE = "request_code";
        private static Bundle _LibBundle;
        private static int _RequestCode;
        public static ObservableCollection<BluetoothDevice> _deviceList;

        public static Context _Context;

        private static NfcAdapter _NfcAdapter;
        private static BluetoothAdapter _BluetoothAdapter;
        private static BluetoothDevice _SelectedDevice;
        public delegate void PairingHandler(object sender, bool bonded, Intent intent, EventArgs e);
        public static event PairingHandler OnDevicePaired;
        private static Activity _Activity;
        private static IntentFilter[] intentFiltersArray;
        private static string[][] techListsArray;

        public static void NewInstance(Context context, Activity activity, int requestCode)
        {
            _LibBundle = new Bundle();
            _LibBundle.PutInt(ARG_REQUEST_CODE, requestCode);
            _RequestCode = requestCode;
            _BluetoothAdapter = BluetoothAdapter.DefaultAdapter;
            _deviceList = GetPairedDeviceCollection();
            _Context = context;
            _Activity = activity;
            _NfcAdapter = NfcAdapter.GetDefaultAdapter(_Context);
            OnDevicePaired += TapAndPair_OnDevicePaired;
        }

        private static ObservableCollection<BluetoothDevice> GetPairedDeviceCollection()
        {
            ICollection<BluetoothDevice> pairedDevices = _BluetoothAdapter.BondedDevices;
            ObservableCollection<BluetoothDevice> deviceList = new ObservableCollection<BluetoothDevice>();
            foreach (BluetoothDevice device in pairedDevices)
            {
                deviceList.Add(device);
            }
            return deviceList;
        }

        public static Intent OnNewIntent(Intent intent)
        {
            var tag = intent.GetParcelableExtra(NfcAdapter.ExtraTag) as Tag;
            var rawMsgs = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);

            if (tag == null)
            {
                return intent;
            }

            if (NfcAdapter.ExtraTag.Contains("nfc"))
            {
                intent = ProcessNfcScan(intent);
            }

            return intent;
        }

        public static void Something()
        {
            PendingIntent pendingIntent = PendingIntent.GetActivity(_Context, 0, new Intent(_Context, _Context.GetType()).AddFlags(ActivityFlags.SingleTop), 0);

            IntentFilter ndef = new IntentFilter(NfcAdapter.ActionNdefDiscovered);
            try
            {
                ndef.AddDataType("*/*");    /* Handles all MIME based dispatches.
                                       You should specify only the ones that you need. */
            }
            catch (MalformedMimeTypeException e)
            {
                throw new Java.Lang.RuntimeException("fail", e);
            }
            intentFiltersArray = new IntentFilter[] { ndef, };
            //techListsArray = new string[][] { new string[] { NfcF.class.getName() }};
        }

        private static void TapAndPair_OnDevicePaired(object sender, bool bonded, Intent intent, EventArgs e)
        {
            _SelectedDevice = sender as BluetoothDevice;
            if (_SelectedDevice != null)
            {
                intent.PutExtra("NFCDeviceName", _SelectedDevice.Name);
                intent.PutExtra("NFCDeviceAddress", _SelectedDevice.Address);
            }
            else
            {
                intent.PutExtra("ErrorMessage", "No device read");
            }
            _SelectedDevice = null;
        }

        private static Intent ProcessNfcScan(Intent intent)
        {
            IParcelable[] scannedTags = intent.GetParcelableArrayExtra(NfcAdapter.ExtraNdefMessages);
            if (scannedTags != null && scannedTags.Length > 0)
            {
                try
                {
                    NdefMessage msg = (NdefMessage)scannedTags[0];
                    byte[] payloadBytes = msg.GetRecords()[0].GetPayload();
                    String payload = String.Empty;
                    foreach (byte b in payloadBytes)
                    {
                        payload += Convert.ToChar(b);
                    }
                    NFCDevice nfcDevice = new NFCDevice
                    {
                        FriendlyName = GetDeviceFriendlyName(payload),
                        MacAddress = GetDeviceMacAddress(payloadBytes),
                    };
                    bool alreadyPaired = false;
                    for (int i=0; i<_deviceList.Count; i++)
                    {
                        if ((_deviceList[i].Name == nfcDevice.FriendlyName) && (_deviceList[i].Address == nfcDevice.MacAddress))
                        {
                            alreadyPaired = true;
                            break;
                        }
                    }
                    if (!alreadyPaired)
                    {
                        intent = PairDevice(nfcDevice, intent);
                    }
                    else
                    {
                        intent.PutExtra("ErrorMessage", "Device Already Paired");
                    }

                }
                catch (Exception ex)
                {
                }

                intent.RemoveExtra(NfcAdapter.ExtraNdefMessages);
            }

            return intent;
        }

        private static Intent PairDevice(NFCDevice nfcDevice, Intent intent)
        {
            BluetoothDevice device = _BluetoothAdapter.GetRemoteDevice(nfcDevice.MacAddress);

            if (device != null)
            {
                OnDevicePaired(device, device.CreateBond(), intent, EventArgs.Empty);
            }
            else
            {
                intent.PutExtra("ErrorMessage", "Failed to communicate with device");
            }

            return intent;
        }

        public static void OnPairDevice(BluetoothDevice device, int state)
        {
            _deviceList.Add(device);
        }

        /// <summary>
        /// Parses out the printer's Friendly Name from the NFC payload
        /// </summary>
        /// <param name="payload"> NFC payload string </param>
        /// <returns> printer's Friendly Name </returns>
        private static string GetDeviceFriendlyName(string payload)
        {
            string parameterFriendlyName = "s=";
            string[] payloadItems = payload.Split('&');
            for (int i = 0; i < payloadItems.Length; i++)
            {
                //Friendly Name
                if (payloadItems[i].StartsWith(parameterFriendlyName, StringComparison.Ordinal))
                {
                    return payloadItems[i].Substring(parameterFriendlyName.Length);
                }
            }
            return "";
        }

        private static string GetDeviceMacAddress(byte[] payloadBytes)
        {
            string parameterFriendlyName = "s=";
            // Get the Language Code
            int languageCodeLength = payloadBytes[0] & 0063;

            string payload = new String(System.Text.UTF8Encoding.ASCII.GetChars(payloadBytes), languageCodeLength + 1, payloadBytes.Length - languageCodeLength - 1);
            string[] payloadItems = payload.Split('&');
            for (int i = 0; i < payloadItems.Length; i++)
            {
                //Mac Address
                if (!payloadItems[i].StartsWith(parameterFriendlyName, StringComparison.Ordinal))
                {
                    return payloadItems[i];
                }
            }
            return "";
        }
    }
}
