#!/bin/bash
set -e

mkdir -p out/win-x64 out/osx-arm64

# RNNoise
git clone --depth 1 https://github.com/xiph/rnnoise.git
cd rnnoise
./autogen.sh

# RNNoise macOS
./configure --disable-static --disable-examples --disable-doc
make -j$(sysctl -n hw.ncpu)
cp .libs/librnnoise.dylib ../out/osx-arm64/
make clean

# RNNoise Windows
./configure --host=x86_64-w64-mingw32 --disable-static --disable-examples --disable-doc
make -j$(sysctl -n hw.ncpu)
cp .libs/librnnoise-0.dll ../out/win-x64/rnnoise.dll
cd ..

# SpeexDSP
git clone --depth 1 https://github.com/xiph/speexdsp.git
cd speexdsp
./autogen.sh

# SpeexDSP macOS
./configure --disable-static
make -j$(sysctl -n hw.ncpu)
cp libspeexdsp/.libs/libspeexdsp.dylib ../out/osx-arm64/
make clean

# SpeexDSP Windows
./configure --host=x86_64-w64-mingw32 --disable-static
make -j$(sysctl -n hw.ncpu)
cp libspeexdsp/.libs/libspeexdsp-1.dll ../out/win-x64/speexdsp.dll
cd ..

echo "Done."
ls -la out/*/
