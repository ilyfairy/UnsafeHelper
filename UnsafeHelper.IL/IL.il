.assembly extern mscorlib
{ }

.assembly UnsafeHelper.IL
{
  .ver 1:0:0:0
}

.module UnsafeHelper.IL.dll

.class public abstract auto ansi sealed beforefieldinit IlyfairyLib.Unsafe.Internal.IL
{
  .method public hidebysig static void* GetPointer(object obj) cil managed aggressiveinlining
  {
    .maxstack 2
    ldarg.0
    ret
  }

  .method public hidebysig static native int GetPointerIntPtr(object obj) cil managed aggressiveinlining
  {
    .maxstack 2
    ldarg.0
    ret
  }
  
  .method public hidebysig static !!T As<class T>(native int objectAddress) cil managed aggressiveinlining
  {
    .maxstack 2
    ldarg.0
    ret
  }
  
  .method public hidebysig static !!T As<class T>(void* objectAddress) cil managed aggressiveinlining
  {
    .maxstack 2
    ldarg.0
    ret
  }
}