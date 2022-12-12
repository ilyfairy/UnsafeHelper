.assembly extern mscorlib
{ }

.assembly UnsafeHelper.IL
{
  .ver 1:0:0:0
}

.module UnsafeHelper.IL.dll

.class public abstract auto ansi sealed beforefieldinit IlyfairyLib.Unsafe.IL
{
  .method public hidebysig static void* GetPointer(object obj) cil managed aggressiveinlining
  {
    .maxstack 2
    ldarg.0
    ret
  }
}