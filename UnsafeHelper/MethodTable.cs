using System.Runtime.InteropServices;

namespace IlyfairyLib.Unsafe
{
//    /// <summary>
//    /// 方法表
//    /// </summary>
//    [StructLayout(LayoutKind.Explicit)]
//    public unsafe struct MethodTable
//    {
//        public const int PtrSize =
//#if TARGET_64BIT
//            8
//#else
//            4
//#endif
//            ;

//        /// <summary>
//        /// <see cref="Array"/> 或 <see langword="string"/> 的每个元素大小
//        /// </summary>
//        [FieldOffset(0)]
//        public ushort ComponentSize; // m_dwFlags lower 16bits

//        /// <summary>
//        /// 低16位表示 <see cref="Array"/> 或 <see langword="string"/> 的每个元素大小
//        /// </summary>
//        [FieldOffset(0)]
//        public uint Flags; // m_dwFlags

//        /// <summary>
//        /// 申请本类型实例时将使用多少字节
//        /// </summary>
//        [FieldOffset(4)]
//        public uint BaseSize; // m_BaseSize

//        // 0x8: m_wFlags2

//        // 0xA: m_wToken

//        /// <summary>
//        /// 虚方法计数
//        /// </summary>
//        [FieldOffset(0xC)]
//        public ushort VirtualsCount; // m_wNumVirtuals

//        /// <summary>
//        /// 实现接口计数
//        /// </summary>
//        [FieldOffset(0xE)]
//        public ushort InterfaceCount; // m_wNumInterfaces

//        /// <summary>
//        /// 父类方法表指针
//        /// </summary>
//        [FieldOffset(0x10)]
//        public MethodTable* ParentMethodTable; // m_pParentMethodTable

//        /// <summary>
//        /// per-instantiation information
//        /// </summary>
//        [FieldOffset(0x10 + 4 * PtrSize)]
//        public void* PerInstInfo; // m_pPerInstInfo

//        /// <summary>
//        /// 数组成员类型的 <see cref="RuntimeTypeHandle.Value"/>
//        /// </summary>
//        [FieldOffset(0x10 + 4 * PtrSize)]
//        public void* ElementType; // m_ElementTypeHnd

//        /// <summary>
//        /// 实现接口方法表
//        /// </summary>
//        [FieldOffset(0x10 + 5 * PtrSize)]
//        public MethodTable** InterfaceMap; // m_pInterfaceMap
//    }

    /// <summary>
    /// Subset of src\vm\methodtable.h
    /// </summary>
    //[StructLayout(LayoutKind.Sequential)]
    public unsafe struct MethodTable
    {
        internal ComponentSize_Flags_0 ComponentSize_Flags;
        /// <summary>
        /// <see cref="System.Array"/>或<see cref="System.String"/>的元素大小
        /// </summary>
        public ref ushort ComponentSize => ref ComponentSize_Flags.ComponentSize; // offset:0
        /// <summary>
        /// 当前<see cref="MethodTable"/>的Flag (仅适用于非<see cref="System.Array"/>或<see cref="System.String"/>)
        /// </summary>
        public ref uint Flags => ref ComponentSize_Flags.Flags; // offset:0

        /// <summary>
        /// 类型的基本大小 (在堆上分配实例时使用)
        /// </summary>
        //[FieldOffset(4)]
        public uint BaseSize; // offset:4

        // 0x8: m_wFlags2
        private ushort m_wFlags2; // offset:8

        // 0xA: m_wToken
        private ushort m_wToken; // offset:10

        /// <summary>
        /// 虚方法个数
        /// </summary>
        //[FieldOffset(0xC)]
        public ushort VirtualsCount; // offset:12

        /// <summary>
        /// 接口个数
        /// </summary>
        //[FieldOffset(0xE)]
        public ushort InterfaceCount; // offset:14

        //private nint debug_m_szClassName; // offset:16

        /// <summary>
        /// 父类的<see cref="MethodTable"/>
        /// </summary>
        //[FieldOffset(0x10)]
        public MethodTable* ParentMethodTable; //offset:16  

        private nint unknown1, unknown2, unknown3;

        /// <summary>
        /// 数组成员类型的<see cref="MethodTable"/>
        /// </summary>
        //[FieldOffset(0x10 + 4 * PtrSize)]
        public void* ElementType; //offset: x86:32  x64:48

        /// <summary>
        /// 实现接口的<see cref="MethodTable"/>
        /// </summary>
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
