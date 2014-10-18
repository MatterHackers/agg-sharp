# Build libSetSerial.so
gcc -shared -fPIC SetSerial.c -o libSetSerial.so -v

# Create path if needed
mkdir -p ../../../MatterControl/bin/Debug/
mkdir -p ../../../MatterControl/bin/Release/

# Copy to MatterControl build directories
cp libSetSerial.so ../../../MatterControl/bin/Debug/
cp libSetSerial.so ../../../MatterControl/bin/Release/
