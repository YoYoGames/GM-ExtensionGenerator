#pragma once

#include <cstdio>

#ifdef OS_WINDOWS
#define GMEXPORT extern "C" __declspec(dllexport)
#elif defined(OS_ANDROID)
#define GMEXPORT
#elif defined(__APPLE__)
#include <TargetConditionals.h>
#if TARGET_OS_IOS || TARGET_OS_TV || defined(OS_IOS) || defined(OS_TVOS)
#define GMEXPORT
#else // macOS, etc.
#define GMEXPORT extern "C" __attribute__((visibility("default")))
#endif
#else
// Linux and others
#define GMEXPORT extern "C" __attribute__((visibility("default")))
#endif

#define TRACE(format, ...)                                                                                             \
    {                                                                                                                  \
        char temp[1024] {};                                                                                            \
        std::snprintf(temp, sizeof(temp), (format), ##__VA_ARGS__);                                                    \
        std::printf("%s :: %s\n", __func__, temp);                                                                     \
        std::fflush(stdout);                                                                                           \
    }

#define LOG(tag, format, ...)                                                                                          \
    {                                                                                                                  \
        char temp[1024] {};                                                                                            \
        std::snprintf(temp, sizeof(temp), (format), ##__VA_ARGS__);                                                    \
        std::printf("[%s] %s :: %s\n", tag, __func__, temp);                                                           \
        std::fflush(stdout);                                                                                           \
    }

#define LOG_ERROR(format, ...) LOG("ERROR", format, ##__VA_ARGS__)
#define LOG_INFO(format, ...) LOG("INFO", format, ##__VA_ARGS__)
#define LOG_WARNING(format, ...) LOG("WARNING", format, ##__VA_ARGS__)
#define LOG_DEBUG(format, ...) LOG("DEBUG", format, ##__VA_ARGS__)

void printBufferHex(const void* buffer, size_t length);