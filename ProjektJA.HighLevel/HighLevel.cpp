#include "JaKernel.h"
#include <cstdint>

namespace
{
    // Maska 3x3 (suma wag = 16 = 2^4)
    constexpr int MASK_3X3[9] = {
        1, 2, 1,
        2, 4, 2,
        1, 2, 1
    };

    // Maska 5x5 (suma wag = 256 = 2^8)
    constexpr int MASK_5X5[25] = {
        1,  4,  6,  4, 1,
        4, 16, 24, 16, 4,
        6, 24, 36, 24, 6,
        4, 16, 24, 16, 4,
        1,  4,  6,  4, 1
    };

    // Maska 7x7 (suma wag = 4096 = 2^12)
    constexpr int MASK_7X7[49] = {
         1,   6,  15,  20,  15,   6,  1,
         6,  36,  90, 120,  90,  36,  6,
        15,  90, 225, 300, 225,  90, 15,
        20, 120, 300, 400, 300, 120, 20,
        15,  90, 225, 300, 225,  90, 15,
         6,  36,  90, 120,  90,  36,  6,
         1,   6,  15,  20,  15,   6,  1
    };
}

extern "C"
{
    const char* JA_GetBackendName() noexcept
    {
        return "C++ HighLevel (Gauss RGB 3x3/5x5/7x7)";
    }

    int JA_ProcessImage(
        const std::uint8_t* in,
        std::uint8_t* out,
        int width,
        int stride,
        int startY,
        int endY,
        int maskSize) noexcept
    {
        if (!in || !out || startY >= endY) return JA_STATUS_INVALID_ARGS;

        const int* mask = MASK_3X3;
        int radius = 1;
        int shift = 4;

        if (maskSize == 5)
        {
            mask = MASK_5X5;
            radius = 2;
            shift = 8;
        }
        else if (maskSize == 7)
        {
            mask = MASK_7X7;
            radius = 3;
            shift = 12;
        }
        else
        {
            maskSize = 3;
        }

        if (width <= maskSize) return JA_STATUS_INVALID_ARGS;

        for (int y = startY; y < endY; ++y)
        {
            for (int x = radius; x < width - radius; ++x)
            {
                for (int c = 0; c < 3; ++c)
                {
                    int sum = 0;
                    int maskIdx = 0;

                    for (int ky = -radius; ky <= radius; ++ky)
                    {
                        const int rowOffset = (y + ky) * stride;
                        for (int kx = -radius; kx <= radius; ++kx)
                        {
                            sum += in[rowOffset + (x + kx) * 3 + c] * mask[maskIdx++];
                        }
                    }

                    out[y * stride + x * 3 + c] = static_cast<std::uint8_t>(sum >> shift);
                }
            }
        }

        return JA_STATUS_OK;
    }
}