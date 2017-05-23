# ObjParser
A WaveFront Obj Parser and Writer in C#

## Summary
As part of the Pyrite3D project I have done significant work with parsing Obj files.  I wanted to share some of that in a more reusable form, so I have extracted the basic parsing and writing of Obj files to a standalone library here.

This is a pretty naive implementation, and only supports V, VT, F and MTLLIB entries in the file, but more can be added very easily.  

All data is parsed into .net generic collections where you can operate on it, then write the file back out.

Also, see http://www.stefangordon.com/parsing-wavefront-obj-files-in-c/
