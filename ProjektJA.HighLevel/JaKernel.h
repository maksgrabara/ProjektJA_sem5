#pragma once

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

    enum JaStatus
    {
        JA_STATUS_OK = 0,
        JA_STATUS_INVALID_ARGS = -1
    };

    const char* JA_GetBackendName() noexcept;

    int JA_ProcessImage(
        const std::uint8_t* in,
        std::uint8_t* out,
        int width,
        int stride,
        int startY,
        int endY,
        int maskSize) noexcept;

#ifdef __cplusplus
}
#endif