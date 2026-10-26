#pragma once

#include <cstdint>

#ifdef __cplusplus
extern "C" {
#endif

	enum class JA_STATUS {
		JA_STATUS_OK = 0,
		JA_INVALID_ARGS = -1
	};

	const char* JA_GetBackendName() noexcept; // Pointer na nazwê wykorzystywanej biblioteki DLL

	int JA_ProcessImage(std::uint8_t* in, std::uint8_t* out, int width, int stride, int startY, int endY, int maskSize) noexcept;

#ifdef __cplusplus
}
#endif