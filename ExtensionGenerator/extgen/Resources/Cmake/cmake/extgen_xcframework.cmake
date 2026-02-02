# cmake/extgen_xcframework.cmake

if(NOT APPLE)
  return()
endif()

# Only relevant for iOS/tvOS builds from a mac host
option(EXTGEN_ENABLE_XCFRAMEWORK_PACKAGING "Add package_* targets" ON)

# Default: build both device + simulator
option(EXTGEN_APPLE_BUILD_SIMULATOR "Build simulator slice (arm64+x86_64)" ON)

set(EXTGEN_APPLE_PACKAGE_CONFIG "Release" CACHE STRING "Debug/Release")
set_property(CACHE EXTGEN_APPLE_PACKAGE_CONFIG PROPERTY STRINGS Debug Release)

if(EXTGEN_ENABLE_XCFRAMEWORK_PACKAGING)
  add_custom_target(
    package_ios_xcframework
    COMMAND ${CMAKE_COMMAND}
      -DPLATFORM=ios
      -DPROJECT_NAME=${PROJECT_NAME}
      -DSRC_DIR=${CMAKE_SOURCE_DIR}
      -DBUILD_CONFIG=${EXTGEN_APPLE_PACKAGE_CONFIG}
      -DBUILD_SIM=${EXTGEN_APPLE_BUILD_SIMULATOR}
      -P ${CMAKE_SOURCE_DIR}/cmake/extgen_package_xcframework.cmake
    VERBATIM
  )

  add_custom_target(
    package_tvos_xcframework
    COMMAND ${CMAKE_COMMAND}
      -DPLATFORM=tvos
      -DPROJECT_NAME=${PROJECT_NAME}
      -DSRC_DIR=${CMAKE_SOURCE_DIR}
      -DBUILD_CONFIG=${EXTGEN_APPLE_PACKAGE_CONFIG}
      -DBUILD_SIM=${EXTGEN_APPLE_BUILD_SIMULATOR}
      -P ${CMAKE_SOURCE_DIR}/cmake/extgen_package_xcframework.cmake
    VERBATIM
  )
endif()
