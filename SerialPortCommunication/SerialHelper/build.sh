# Build libSetSerial.so
gcc -m32 -shared -fPIC SetSerial.c -o libSetSerial.so -v

# Create path if needed
mkdir -p ../../../../bin/Debug/
mkdir -p ../../../../bin/Release/

# Copy to MatterControl build directories
cp libSetSerial.so ../../../../bin/Debug/
cp libSetSerial.so ../../../../bin/Release/
