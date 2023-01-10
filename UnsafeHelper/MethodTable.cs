using System.Runtime.InteropServices;

namespace IlyfairyLib.Unsafe
{
    //    [StructLayout(LayoutKind.Explicit)]
    //    public unsafe struct MethodTable
    //    {
    //        public const int PtrSize =
    //#if TARGET_64BIT
    //        8
    //#else
    //            4
    //#endif
    //            ;

    //        [FieldOffset(0)]
    //        public ushort ComponentSize;

    //        [FieldOffset(0)]
    //        public uint Flags;

    //        [FieldOffset(4)]
    //        public uint BaseSize;

    //        // 0x8: m_wFlags2

    //        // 0xA: m_wToken

    //        [FieldOffset(0xC)]
    //        public ushort VirtualsCount;

    //        [FieldOffset(0xE)]
    //        public ushort InterfaceCount;

    //        [FieldOffset(0x10)]
    //        public MethodTable* ParentMethodTable;

    //        [FieldOffset(0x10 + 4 * PtrSize)]
    //        public void* ElementType;

    //        [FieldOffset(0x10 + 5 * PtrSize)]
    //        public MethodTable** InterfaceMap;
    //    }



    /// <summary>
    /// Subset of src\vm\methodtable.h
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct MethodTable
    {
        internal ComponentSize_Flags_0 ComponentSize_Flags;
        public ref ushort ComponentSize => ref ComponentSize_Flags.ComponentSize; // offset:0
        public ref uint Flags => ref ComponentSize_Flags.Flags; // offset:0

        //[FieldOffset(4)]
        public uint BaseSize; // offset:4

        // 0x8: m_wFlags2
        private ushort m_wFlags2; // offset:8

        // 0xA: m_wToken
        private ushort m_wToken; // offset:10

        //[FieldOffset(0xC)]
        public ushort VirtualsCount; // offset:12

        //[FieldOffset(0xE)]
        public ushort InterfaceCount; // offset:14

        //private nint debug_m_szClassName; // offset:16

        ////[FieldOffset(0x10)]
        public MethodTable* ParentMethodTable; //offset:16  

        private nint unknown1, unknown2, unknown3;

        //[FieldOffset(0x10 + 4 * PtrSize)]
        public void* ElementType; //offset: x86:32  x64:48

        //[FieldOffset(0x10 + 5 * PtrSize)]
        public MethodTable** InterfaceMap; //offset: x86:36  x64:56

        [StructLayout(LayoutKind.Explicit, Size = 4)]
        internal struct ComponentSize_Flags_0
        {
            [FieldOffset(0)]
            public ushort ComponentSize;

            [FieldOffset(0)]
            public uint Flags;
        }
    }
}
