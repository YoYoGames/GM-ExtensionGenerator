# cmake/extgen_package_xcframework.cmake

if(NOT DEFINED PLATFORM)
  message(FATAL_ERROR "PLATFORM must be ios or tvos")
endif()
if(NOT DEFINED PROJECT_NAME OR NOT DEFINED SRC_DIR)
  message(FATAL_ERROR "PROJECT_NAME and SRC_DIR are required")
endif()

# Pull public header lists from cache (set by src/CMakeLists.txt)
if(PLATFORM STREQUAL "ios")
  set(PUBLIC_HEADERS ${EXT_PUBLIC_HEADERS_IOS})
  set(SDK_DEVICE "iphoneos")
  set(SDK_SIM "iphonesimulator")
  set(DROP_DIR "${SRC_DIR}/../iOSSourceFromMac")
elseif(PLATFORM STREQUAL "tvos")
  set(PUBLIC_HEADERS ${EXT_PUBLIC_HEADERS_TVOS})
  set(SDK_DEVICE "appletvos")
  set(SDK_SIM "appletvsimulator")
  set(DROP_DIR "${SRC_DIR}/../tvOSSourceFromMac")
else()
  message(FATAL_ERROR "Unknown PLATFORM=${PLATFORM}")
endif()

if(NOT BUILD_CONFIG)
  set(BUILD_CONFIG "Release")
endif()

set(BUILD_ROOT "${SRC_DIR}/out/_xcbuild_${PLATFORM}")
set(DEVICE_DIR "${BUILD_ROOT}/device")
set(SIM_DIR "${BUILD_ROOT}/sim")
set(OUT_DIR "${SRC_DIR}/out/${PLATFORM}_xcframework")

file(REMOVE_RECURSE "${BUILD_ROOT}")
file(MAKE_DIRECTORY "${OUT_DIR}")
file(REMOVE_RECURSE "${OUT_DIR}/${PROJECT_NAME}.xcframework")
file(REMOVE "${OUT_DIR}/${PROJECT_NAME}.zip")

# Collect headers into a folder (xcodebuild requires a folder)
set(HDRS_DIR "${BUILD_ROOT}/headers")
file(MAKE_DIRECTORY "${HDRS_DIR}")

foreach(h IN LISTS PUBLIC_HEADERS)
  if(NOT EXISTS "${h}")
    message(FATAL_ERROR "Public header does not exist: ${h}")
  endif()
  file(COPY "${h}" DESTINATION "${HDRS_DIR}")
endforeach()

# ---- configure+build device
execute_process(
  COMMAND ${CMAKE_COMMAND} -S "${SRC_DIR}" -B "${DEVICE_DIR}" -G Xcode
    -DCMAKE_SYSTEM_NAME=Darwin
    -DCMAKE_OSX_SYSROOT=${SDK_DEVICE}
    -DCMAKE_OSX_ARCHITECTURES=arm64
    -DCMAKE_BUILD_TYPE=${BUILD_CONFIG}
    -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
  RESULT_VARIABLE r1
)
if(NOT r1 EQUAL 0)
  message(FATAL_ERROR "${PLATFORM} device configure failed")
endif()

execute_process(
  COMMAND ${CMAKE_COMMAND} --build "${DEVICE_DIR}" --config ${BUILD_CONFIG}
  RESULT_VARIABLE r2
)
if(NOT r2 EQUAL 0)
  message(FATAL_ERROR "${PLATFORM} device build failed")
endif()

# ---- configure+build simulator (optional)
set(HAVE_SIM FALSE)
if(BUILD_SIM)
  execute_process(
    COMMAND ${CMAKE_COMMAND} -S "${SRC_DIR}" -B "${SIM_DIR}" -G Xcode
      -DCMAKE_SYSTEM_NAME=Darwin
      -DCMAKE_OSX_SYSROOT=${SDK_SIM}
      -DCMAKE_OSX_ARCHITECTURES=arm64;x86_64
      -DCMAKE_BUILD_TYPE=${BUILD_CONFIG}
      -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
    RESULT_VARIABLE r3
  )
  if(NOT r3 EQUAL 0)
    message(FATAL_ERROR "${PLATFORM} simulator configure failed")
  endif()

  execute_process(
    COMMAND ${CMAKE_COMMAND} --build "${SIM_DIR}" --config ${BUILD_CONFIG}
    RESULT_VARIABLE r4
  )
  if(NOT r4 EQUAL 0)
    message(FATAL_ERROR "${PLATFORM} simulator build failed")
  endif()

  set(HAVE_SIM TRUE)
endif()

# ---- locate built static libs
set(DEV_LIB "${DEVICE_DIR}/${BUILD_CONFIG}-${SDK_DEVICE}/lib${PROJECT_NAME}.a")

if(NOT EXISTS "${DEV_LIB}")
  message(FATAL_ERROR "Missing device static library: ${DEV_LIB}")
endif()

if(HAVE_SIM)
  set(SIM_LIB "${SIM_DIR}/${BUILD_CONFIG}-${SDK_SIM}/lib${PROJECT_NAME}.a")
  if(NOT EXISTS "${SIM_LIB}")
    message(FATAL_ERROR "Missing simulator static library: ${SIM_LIB}")
  endif()
endif()

# ---- create xcframework
if(HAVE_SIM)
  execute_process(
    COMMAND xcodebuild -create-xcframework
      -library "${DEV_LIB}" -headers "${HDRS_DIR}"
      -library "${SIM_LIB}" -headers "${HDRS_DIR}"
      -output "${OUT_DIR}/${PROJECT_NAME}.xcframework"
    RESULT_VARIABLE r5
  )
else()
  execute_process(
    COMMAND xcodebuild -create-xcframework
      -library "${DEV_LIB}" -headers "${HDRS_DIR}"
      -output "${OUT_DIR}/${PROJECT_NAME}.xcframework"
    RESULT_VARIABLE r5
  )
endif()

if(NOT r5 EQUAL 0)
  message(FATAL_ERROR "xcframework create failed")
endif()

# ---- zip it
execute_process(
  COMMAND "${CMAKE_COMMAND}" -E tar "cf" "${OUT_DIR}/${PROJECT_NAME}.zip" --format=zip "${PROJECT_NAME}.xcframework"
  WORKING_DIRECTORY "${OUT_DIR}"
  RESULT_VARIABLE rz
)
if(NOT rz EQUAL 0)
  message(FATAL_ERROR "Failed to create zip")
endif()

file(MAKE_DIRECTORY "${DROP_DIR}")
file(REMOVE "${DROP_DIR}/${PROJECT_NAME}.zip")
file(COPY "${OUT_DIR}/${PROJECT_NAME}.zip" DESTINATION "${DROP_DIR}")

message(STATUS "Wrote: ${DROP_DIR}/${PROJECT_NAME}.zip")
