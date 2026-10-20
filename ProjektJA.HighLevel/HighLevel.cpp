#include "JaKernel.h"
#include <thread>
#include <vector>
#include <cstring>
#include <cstdint>

#include "JaKernel.h"

extern "C" {
    const char* JA_GetBackendName() noexcept {
        return "C++ HighLevel (Gauss 3x3)";
    }

    constexpr int W_CORNER = 1;
    constexpr int W_EDGE = 2;
    constexpr int W_CENTER = 4;
    constexpr int NORMALIZATION_SHIFT = 4;

    int JA_ProcessImage(const std::uint8_t* in, std::uint8_t* out, int width, int stride, int startY, int endY) noexcept {
        if (!in || !out || width < 3 || startY >= endY) return -1;

        for (int y = startY; y < endY; ++y) {
            for (int x = 1; x < width - 1; ++x) {
                // Skala szarości to i tak wymuszone 3 kanały z racji struktury BGR, wykonujemy maskę osobno
                for (int c = 0; c < 3; ++c) {
                    int sum = 0;
                    sum += in[(y - 1) * stride + (x - 1) * 3 + c] * W_CORNER;
                    sum += in[(y - 1) * stride + x * 3 + c] * W_EDGE;
                    sum += in[(y - 1) * stride + (x + 1) * 3 + c] * W_CORNER;

                    sum += in[y * stride + (x - 1) * 3 + c] * W_EDGE;
                    sum += in[y * stride + x * 3 + c] * W_CENTER;
                    sum += in[y * stride + (x + 1) * 3 + c] * W_EDGE;

                    sum += in[(y + 1) * stride + (x - 1) * 3 + c] * W_CORNER;
                    sum += in[(y + 1) * stride + x * 3 + c] * W_EDGE;
                    sum += in[(y + 1) * stride + (x + 1) * 3 + c] * W_CORNER;

                    out[y * stride + x * 3 + c] = static_cast<std::uint8_t>(sum >> NORMALIZATION_SHIFT);
                }
            }
        }
        return 0; // JA_STATUS_OK
    }
}