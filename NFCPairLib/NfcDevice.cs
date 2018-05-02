using System;

namespace com.touchstar.chrisd.NFCPairLib
{
    public interface INFCDevice
    {
        String FriendlyName { get; set; }
        String MacAddress { get; set; }
    }

    internal class NFCDevice : INFCDevice
    {
        public String FriendlyName { get; set; }
        public String MacAddress { get; set; }
        public int PIN { get; set; }
    }
}