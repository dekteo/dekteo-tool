$ErrorActionPreference='Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ShaderCompiler {
 [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 public interface Blob { [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] UIntPtr GetBufferSize(); }
 [DllImport("d3dcompiler_47.dll", CharSet=CharSet.Ansi)]
 public static extern int D3DCompile(byte[] data, UIntPtr length, string name, IntPtr defines, IntPtr include, string entry, string target, uint flags, uint flags2, out Blob code, out Blob errors);
 public static void Compile(string source, string output) {
  byte[] data=System.IO.File.ReadAllBytes(source); Blob code, errors;
  int result=D3DCompile(data,(UIntPtr)data.Length,source,IntPtr.Zero,IntPtr.Zero,"main","ps_2_0",0,0,out code,out errors);
  if(result<0) throw new Exception(errors==null?result.ToString():Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
  byte[] bytes=new byte[(int)code.GetBufferSize().ToUInt64()]; Marshal.Copy(code.GetBufferPointer(),bytes,0,bytes.Length);
  System.IO.File.WriteAllBytes(output,bytes); Marshal.ReleaseComObject(code); if(errors!=null)Marshal.ReleaseComObject(errors);
 }
}
'@
$shaderDirectory=Join-Path $PSScriptRoot '../VeadotubeMy/VeadotubeMy/Shaders'
[ShaderCompiler]::Compile((Join-Path $shaderDirectory 'ColorCorrection.hlsl'),(Join-Path $shaderDirectory 'ColorCorrection.ps'))
