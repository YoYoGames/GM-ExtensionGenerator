
#include "GMExtUtils.h"
#include <cstdint>
#include <iostream>
#include <iomanip>

void printBufferHex(const void* buffer, size_t length)
{
    const uint8_t* byteBuffer = static_cast<const uint8_t*>(buffer);

    std::cout << std::hex << std::setfill('0');
    for (size_t i = 0; i < length; ++i) {
        std::cout << std::setw(2) << static_cast<unsigned>(byteBuffer[i]);
        if (i < length - 1) {
            std::cout << " ";
        }
    }
    std::cout << std::dec << std::endl; // Switch back to decimal output
}