cmake_minimum_required(VERSION 3.18)
project(${EXTGEN_EXTENSION_NAME} LANGUAGES CXX)

# ============================================================
# Global language / options
# ============================================================
set(CMAKE_CXX_STANDARD ${EXTGEN_CPP_VERSION})
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS ${EXTGEN_CPP_EXTENSIONS})
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

option(EXT_STRICT_WARNINGS "Enable -Wall/-Wextra or /W4"
       ${EXTGEN_STRICT_WARNINGS})
option(EXT_USE_THIRD_PARTY "Enable third_party directory"
       ${EXTGEN_USE_THIRD_PARTY})

# Apple packaging behavior (framework vs static lib for iOS/tvOS)
option(EXT_IOS_AS_FRAMEWORK
       "Build iOS target as a framework (ON) or static lib (OFF)" ON)
option(EXT_TVOS_AS_FRAMEWORK
       "Build tvOS target as a framework (ON) or static lib (OFF)" ON)

# Use alternate Apple source roots intended for 'native' redirected builds
option(EXT_APPLE_NATIVE_FRAMEWORK
       "Use Apple NATIVE flavour (ObjC wrapper + shared C++ native core)" OFF)

# App usually builds these itself on iOS/tvOS when your lib is static.
option(EXT_EMBED_CORE_SRC "Compile code_gen/core sources into lib" ON)

set(EXT_APPLE_PACKAGE_CONFIG
    "Release"
    CACHE STRING "Config used by xcframework packagers (Debug/Release)")
set_property(CACHE EXT_APPLE_PACKAGE_CONFIG PROPERTY STRINGS Debug Release)

# Disable code signing for Xcode-generated projects (device & sim)
if(APPLE AND CMAKE_GENERATOR STREQUAL "Xcode")
  # Global defaults (can still be overridden per target if needed)
  set(CMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_ALLOWED "NO")
  set(CMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_REQUIRED "NO")
  set(CMAKE_XCODE_ATTRIBUTE_CODE_SIGNING_STYLE "Manual")
  set(CMAKE_XCODE_ATTRIBUTE_DEVELOPMENT_TEAM "")
  set(CMAKE_XCODE_ATTRIBUTE_PROVISIONING_PROFILE_SPECIFIER "")
endif()

# ============================================================
# Apple / Console platform detection
# ============================================================

set(EXTGEN_APPLE_IOS FALSE)
set(EXTGEN_APPLE_TVOS FALSE)

if(APPLE)
  if(CMAKE_OSX_SYSROOT MATCHES "iphone") # iphoneos / iphonesimulator
    set(EXTGEN_APPLE_IOS TRUE)
  elseif(CMAKE_OSX_SYSROOT MATCHES "appletv") # appletvos / appletvsimulator
    set(EXTGEN_APPLE_TVOS TRUE)
  endif()
endif()

set(EXTGEN_XBOX FALSE)
set(EXTGEN_PS4 FALSE)
set(EXTGEN_PS5 FALSE)
set(EXTGEN_SWITCH FALSE)

if(MSVC
   AND DEFINED CMAKE_VS_PLATFORM_NAME
   AND CMAKE_VS_PLATFORM_NAME MATCHES "^Gaming\\.Xbox")
  set(EXTGEN_XBOX TRUE)
endif()
if(CMAKE_SYSTEM_NAME STREQUAL "ORBIS") # PS4
  set(EXTGEN_PS4 TRUE)
elseif(CMAKE_SYSTEM_NAME STREQUAL "Prospero") # PS5
  set(EXTGEN_PS5 TRUE)
endif()
if(CMAKE_SYSTEM_NAME STREQUAL "NintendoSwitch" OR DEFINED
                                                  ENV{NINTENDO_SDK_ROOT})
  set(EXTGEN_SWITCH TRUE)
endif()

# ============================================================
# Target kind
# ============================================================
# iOS/tvOS: build either a Framework (shared bundle) or a static library, per
# options.
set(_make_framework FALSE)
if(EXTGEN_APPLE_IOS AND EXT_IOS_AS_FRAMEWORK)
  set(_make_framework TRUE)
elseif(EXTGEN_APPLE_TVOS AND EXT_TVOS_AS_FRAMEWORK)
  set(_make_framework TRUE)
endif()

if(_make_framework)
  add_library(${PROJECT_NAME} SHARED) # becomes a Framework via PROPERTIES
  set_target_properties(${PROJECT_NAME} PROPERTIES FRAMEWORK TRUE
                                                   FRAMEWORK_VERSION A)
elseif(EXTGEN_APPLE_IOS OR EXTGEN_APPLE_TVOS)
  add_library(${PROJECT_NAME} STATIC) # iOS/tvOS non-framework builds => STATIC
else()
  add_library(${PROJECT_NAME} SHARED) # desktop/Android default
endif()

# If we're on iOS/tvOS and building a static library (not a Framework), default
# to OFF to avoid duplicate symbols.
if((EXTGEN_APPLE_IOS OR EXTGEN_APPLE_TVOS) AND NOT _make_framework)
  set(EXT_EMBED_CORE_SRC OFF)
endif()

set_target_properties(${PROJECT_NAME} PROPERTIES POSITION_INDEPENDENT_CODE ON
                                                 OUTPUT_NAME "${PROJECT_NAME}")

# ============================================================
# Includes & source discovery
# ============================================================
set(SRC_CORE ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/core/GMExtUtils.cpp
             ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/core/GMExtWire.cpp)

if(EXT_EMBED_CORE_SRC)
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_CORE})
endif()

target_include_directories(
  ${PROJECT_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/code_gen
                          ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/core)

file(GLOB SRC_COMMON CONFIGURE_DEPENDS
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/*.cpp)
target_sources(${PROJECT_NAME} PRIVATE ${SRC_COMMON})

file(
  GLOB_RECURSE
  SRC_ANDROID
  CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/android/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/android/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp)

file(GLOB_RECURSE SRC_WINDOWS CONFIGURE_DEPENDS
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp)

file(GLOB_RECURSE SRC_MACOS CONFIGURE_DEPENDS
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp)

file(
  GLOB_RECURSE
  SRC_IOS
  CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.mm
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.swift)

file(
  GLOB_RECURSE
  SRC_TVOS
  CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.mm
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.swift)

# Native Apple framework source roots
file(
  GLOB_RECURSE
  SRC_IOS_NATIVE
  CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.mm
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/*.swift)

file(
  GLOB_RECURSE
  SRC_TVOS_NATIVE
  CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.mm
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/*.swift)

file(GLOB_RECURSE SRC_LINUX CONFIGURE_DEPENDS
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
     ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp)

add_subdirectory("${CMAKE_CURRENT_SOURCE_DIR}/src")

# ============================================================
# Platform selection & defines
# ============================================================
if(ANDROID)
  message(STATUS "Including Android sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_ANDROID})
  target_include_directories(
    ${PROJECT_NAME} PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/android)
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_ANDROID
                                                     EXTGEN_HAS_JNI=1)
  find_library(_log_lib log)
  find_library(_android_lib android)
  target_link_libraries(${PROJECT_NAME} PRIVATE ${_log_lib} ${_android_lib})
  message(STATUS "Android ABI=${CMAKE_ANDROID_ARCH_ABI}, STL=c++_static")

elseif(WIN32)
  message(STATUS "Including Windows sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_WINDOWS})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_WINDOWS
                                                     EXTGEN_HAS_JNI=0)

elseif(APPLE AND EXTGEN_APPLE_IOS)
  message(STATUS "Including iOS sources")
  enable_language(OBJCXX)
  enable_language(Swift)
  if(EXT_APPLE_NATIVE_FRAMEWORK)
    message(STATUS "iOS flavour: NATIVE (ObjC wrapper + C++ native core)")
    target_sources(${PROJECT_NAME} PRIVATE ${SRC_IOS_NATIVE})
    target_compile_definitions(${PROJECT_NAME} PRIVATE EXTGEN_APPLE_NATIVE_FW=1)
  else()
    message(STATUS "iOS flavour: STANDARD (platform-native implementation)")
    target_sources(${PROJECT_NAME} PRIVATE ${SRC_IOS})
    target_compile_definitions(${PROJECT_NAME} PRIVATE EXTGEN_APPLE_NATIVE_FW=0)
  endif()
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_IOS EXTGEN_HAS_JNI=0)
  if(_make_framework)
    set(_IOS_PUBLIC_HEADERS
        "${CMAKE_CURRENT_SOURCE_DIR}/src/ios/${PROJECT_NAME}_ios.h"
        "${CMAKE_CURRENT_SOURCE_DIR}/code_gen/ios/${PROJECT_NAME}Internal_ios.h"
    )

    # Optional: pick a public header to surface
    set_target_properties(
      ${PROJECT_NAME}
      PROPERTIES MACOSX_FRAMEWORK_IDENTIFIER "com.gamemaker.${PROJECT_NAME}"
                 PUBLIC_HEADER "${_IOS_PUBLIC_HEADERS}")
  endif()

elseif(APPLE AND EXTGEN_APPLE_TVOS)
  message(STATUS "Including tvOS sources")
  enable_language(OBJCXX)
  enable_language(Swift)
  if(EXT_APPLE_NATIVE_FRAMEWORK)
    message(STATUS "tvOS flavour: NATIVE (ObjC wrapper + C++ native core)")
    target_sources(${PROJECT_NAME} PRIVATE ${SRC_TVOS_NATIVE})
    target_compile_definitions(${PROJECT_NAME} PRIVATE EXTGEN_APPLE_NATIVE_FW=1)
  else()
    message(STATUS "tvOS flavour: STANDARD (platform-native implementation)")
    target_sources(${PROJECT_NAME} PRIVATE ${SRC_TVOS})
    target_compile_definitions(${PROJECT_NAME} PRIVATE EXTGEN_APPLE_NATIVE_FW=0)
  endif()
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_TVOS EXTGEN_HAS_JNI=0)

  if(_make_framework)
    set(_TVOS_PUBLIC_HEADERS
        "${CMAKE_CURRENT_SOURCE_DIR}/src/tvos/${PROJECT_NAME}_tvos.h"
        "${CMAKE_CURRENT_SOURCE_DIR}/code_gen/tvos/${PROJECT_NAME}Internal_tvos.h"
    )

    set_target_properties(
      ${PROJECT_NAME}
      PROPERTIES MACOSX_FRAMEWORK_IDENTIFIER "com.gamemaker.${PROJECT_NAME}"
                 PUBLIC_HEADER "${_TVOS_PUBLIC_HEADERS}")
  endif()

elseif(APPLE)
  message(STATUS "Including macOS sources")
  enable_language(OBJCXX)
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_MACOS})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_MACOS EXTGEN_HAS_JNI=0)

elseif(UNIX)
  message(STATUS "Including Linux sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_LINUX})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_LINUX EXTGEN_HAS_JNI=0)

elseif(EXTGEN_XBOX)
  message(STATUS "Including Xbox sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_WINDOWS})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_XBOX EXTGEN_HAS_JNI=0)

elseif(EXTGEN_PS4)
  message(STATUS "Including PS4 sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_LINUX})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_PS4 EXTGEN_HAS_JNI=0)

elseif(EXTGEN_PS5)
  message(STATUS "Including PS5 sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_LINUX})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_PS5 EXTGEN_HAS_JNI=0)

elseif(EXTGEN_SWITCH)
  message(STATUS "Including Nintendo Switch sources")
  target_sources(${PROJECT_NAME} PRIVATE ${SRC_LINUX})
  target_compile_definitions(${PROJECT_NAME} PRIVATE OS_SWITCH EXTGEN_HAS_JNI=0)
endif()

# ============================================================
# Warnings
# ============================================================
if(EXT_STRICT_WARNINGS)
  if(MSVC)
    target_compile_options(${PROJECT_NAME} PRIVATE /W4 /permissive-)
    target_compile_definitions(
      ${PROJECT_NAME} PRIVATE _CRT_SECURE_NO_WARNINGS WIN32_LEAN_AND_MEAN
                              NOMINMAX)
  else()
    target_compile_options(${PROJECT_NAME} PRIVATE -Wall -Wextra -Wpedantic)
    if(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
      target_compile_options(${PROJECT_NAME} PRIVATE -Wno-unknown-pragmas)
    endif()
  endif()
endif()

# ============================================================
# third_party
# ============================================================
set(EXT_THIRDPARTY_DIR "${CMAKE_CURRENT_SOURCE_DIR}/third_party")
if(EXT_USE_THIRD_PARTY AND EXISTS "${EXT_THIRDPARTY_DIR}/CMakeLists.txt")
  message(STATUS "Loading third_party from: ${EXT_THIRDPARTY_DIR}")

  # Tell the third_party CMake which target it should inject sources into
  set(EXT_MAIN_TARGET "${PROJECT_NAME}")

  add_subdirectory("${EXT_THIRDPARTY_DIR}")

  if(TARGET ext_third_party)
    target_link_libraries(${PROJECT_NAME} PRIVATE ext_third_party)
  endif()
else()
  message(STATUS "No third_party directory (or disabled).")
endif()

# ============================================================
# RPATH for *nix (not iOS/tvOS)
# ============================================================
if(APPLE
   AND NOT EXTGEN_APPLE_IOS
   AND NOT EXTGEN_APPLE_TVOS)
  set_target_properties(
    ${PROJECT_NAME}
    PROPERTIES MACOSX_RPATH ON
               BUILD_WITH_INSTALL_RPATH ON
               INSTALL_RPATH "@loader_path")
elseif(UNIX AND NOT ANDROID)
  set_target_properties(${PROJECT_NAME} PROPERTIES BUILD_WITH_INSTALL_RPATH ON
                                                   INSTALL_RPATH "$ORIGIN")
endif()

# ============================================================
# Outputs / post-build copies
# ============================================================
set(_EXT_OUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../")

if(ANDROID)
  set(_ANDROID_OUT_BASE "${CMAKE_CURRENT_SOURCE_DIR}/../AndroidSource/libs")
  set(_ANDROID_ABI_DIR "${_ANDROID_OUT_BASE}/${CMAKE_ANDROID_ARCH_ABI}")

  add_custom_command(
    TARGET ${PROJECT_NAME}
    POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${_ANDROID_ABI_DIR}"
    COMMAND
      ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:${PROJECT_NAME}>"
      "${_ANDROID_ABI_DIR}/lib$<TARGET_PROPERTY:${PROJECT_NAME},OUTPUT_NAME>.so"
    COMMENT "Copying Android shared library to ${_ANDROID_ABI_DIR}")

  if(DEFINED EXT_TP_ANDROID_AAR AND EXISTS "${EXT_TP_ANDROID_AAR}")
    set(_ANDROID_AAR_DIR
        "${CMAKE_CURRENT_SOURCE_DIR}/../AndroidSource/libs-aar")
    add_custom_command(
      TARGET ${PROJECT_NAME}
      POST_BUILD
      COMMAND ${CMAKE_COMMAND} -E make_directory "${_ANDROID_AAR_DIR}"
      COMMAND ${CMAKE_COMMAND} -E copy_if_different "${EXT_TP_ANDROID_AAR}"
              "${_ANDROID_AAR_DIR}/"
      COMMENT "Copying third-party AAR to ${_ANDROID_AAR_DIR}")
  endif()
  if(EXT_TP_RUNTIME_DLLS)
    add_custom_command(
      TARGET ${PROJECT_NAME}
      POST_BUILD
      COMMAND ${CMAKE_COMMAND} -E copy_if_different ${EXT_TP_RUNTIME_DLLS}
              "${_ANDROID_ABI_DIR}/"
      COMMENT "Copying third-party Android .so(s) to ${_ANDROID_ABI_DIR}")
  endif()

elseif(EXTGEN_APPLE_IOS OR EXTGEN_APPLE_TVOS)
  message(
    STATUS
      "Apple mobile build leaves the product in the build dir. Use the xcframework targets below to package device+sim."
  )

else()
  # Always copy the built library itself
  add_custom_command(
    TARGET ${PROJECT_NAME}
    POST_BUILD
    COMMAND
      "${CMAKE_COMMAND}" -E copy_if_different "$<TARGET_FILE:${PROJECT_NAME}>"
      "${_EXT_OUT_DIR}/$<TARGET_FILE_NAME:${PROJECT_NAME}>"
    COMMENT "Copying shared library to ${_EXT_OUT_DIR}")

  # ---- Copy dependent runtime DLLs (Windows) only if any exist
  if(WIN32 AND CMAKE_VERSION VERSION_GREATER_EQUAL 3.21)
    # Write a small helper that no-ops if the list is empty
    file(
      WRITE "${CMAKE_CURRENT_BINARY_DIR}/_copy_runtime_dlls.cmake"
      [=[
      if(NOT DEFINED dlls OR dlls STREQUAL "")
        message(STATUS "No runtime DLLs to copy")
        return()
      endif()
      foreach(f IN LISTS dlls)
        if(EXISTS "${f}")
          file(COPY "${f}" DESTINATION "${dst}")
        endif()
      endforeach()
    ]=])

    add_custom_command(
      TARGET ${PROJECT_NAME}
      POST_BUILD
      COMMAND
        "${CMAKE_COMMAND}" -Ddlls="$<TARGET_RUNTIME_DLLS:${PROJECT_NAME}>"
        -Ddst="${_EXT_OUT_DIR}" -P
        "${CMAKE_CURRENT_BINARY_DIR}/_copy_runtime_dlls.cmake"
      COMMENT "Copying dependent runtime DLLs (if any)")
  endif()

  # ---- Third-party runtime payloads (guard against empty)
  if(DEFINED EXT_TP_RUNTIME_DLLS AND EXT_TP_RUNTIME_DLLS)
    add_custom_command(
      TARGET ${PROJECT_NAME}
      POST_BUILD
      COMMAND "${CMAKE_COMMAND}" -E copy_if_different ${EXT_TP_RUNTIME_DLLS}
              "${_EXT_OUT_DIR}/"
      COMMAND_EXPAND_LISTS
      COMMENT "Copying third-party runtime(s)")
  endif()
endif()

# ============================================================
# Apple xcframework packagers (no preexisting Xcode project needed) They spin up
# two out-of-source Xcode builds (device + sim) then call xcodebuild
# -create-xcframework
# ============================================================
if(APPLE)
  # Drill: build helper that configures & builds with Xcode for a given SDK
  function(_ext_cfg_and_build _dir _sysroot)
    execute_process(
      COMMAND
        ${CMAKE_COMMAND} -S ${CMAKE_SOURCE_DIR} -B ${_dir} -G Xcode
        -DCMAKE_SYSTEM_NAME=Darwin -DCMAKE_OSX_SYSROOT=${_sysroot}
        -DCMAKE_OSX_ARCHITECTURES=arm64;x86_64 -DCMAKE_BUILD_TYPE=Release
        -DEXT_IOS_AS_FRAMEWORK=${EXT_IOS_AS_FRAMEWORK}
        -DEXT_TVOS_AS_FRAMEWORK=${EXT_TVOS_AS_FRAMEWORK}
        -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
      RESULT_VARIABLE _cfg_r)
    if(NOT _cfg_r EQUAL 0)
      message(FATAL_ERROR "Configure failed for ${_dir}")
    endif()
    execute_process(COMMAND ${CMAKE_COMMAND} --build ${_dir} --config Release
                    RESULT_VARIABLE _bld_r)
    if(NOT _bld_r EQUAL 0)
      message(FATAL_ERROR "Build failed for ${_dir}")
    endif()
  endfunction()

  # ---- iOS xcframework target
  add_custom_target(
    package_ios_xcframework
    COMMAND ${CMAKE_COMMAND} -E cmake_echo_color --cyan
            "Packaging iOS xcframework (device + simulator)"
    COMMAND ${CMAKE_COMMAND} -DPLAT=iOS -P
            "${CMAKE_BINARY_DIR}/_package_ios.cmake"
    BYPRODUCTS
      "${CMAKE_BINARY_DIR}/../ios_xcframework/${PROJECT_NAME}.xcframework"
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}"
    VERBATIM)

  # Where to drop the zipped packages after creation
  set(IOS_DROP_DIR
      "${CMAKE_CURRENT_SOURCE_DIR}/../iOSSourceFromMac"
      CACHE PATH "iOS zip drop folder")
  set(TVOS_DROP_DIR
      "${CMAKE_CURRENT_SOURCE_DIR}/../tvOSSourceFromMac"
      CACHE PATH "tvOS zip drop folder")

  file(
    WRITE "${CMAKE_CURRENT_BINARY_DIR}/_package_ios.cmake"
    "
    set(DEVICE_DIR \"${CMAKE_BINARY_DIR}/_ios_device\")
    set(SIM_DIR    \"${CMAKE_BINARY_DIR}/_ios_sim\")
    file(REMOVE_RECURSE \"\${DEVICE_DIR}\" \"\${SIM_DIR}\")
    # configure & build both
    execute_process(COMMAND \"${CMAKE_COMMAND}\" -S \"${CMAKE_SOURCE_DIR}\" -B \"\${DEVICE_DIR}\" -G Xcode
                    -DCMAKE_SYSTEM_NAME=Darwin -DCMAKE_OSX_SYSROOT=iphoneos -DCMAKE_OSX_ARCHITECTURES=arm64
                    -DCMAKE_BUILD_TYPE=${EXT_APPLE_PACKAGE_CONFIG} -DEXT_IOS_AS_FRAMEWORK=${EXT_IOS_AS_FRAMEWORK}
                    -DEXT_TVOS_AS_FRAMEWORK=${EXT_TVOS_AS_FRAMEWORK} -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
                    -DEXT_APPLE_NATIVE_FRAMEWORK=${EXT_APPLE_NATIVE_FRAMEWORK}
                    RESULT_VARIABLE r1)
    if(NOT r1 EQUAL 0)
      message(FATAL_ERROR \"iOS device configure failed\")
    endif()
    execute_process(COMMAND \"${CMAKE_COMMAND}\" --build \"\${DEVICE_DIR}\" --config ${EXT_APPLE_PACKAGE_CONFIG} RESULT_VARIABLE r2)
    if(NOT r2 EQUAL 0)
      message(FATAL_ERROR \"iOS device build failed\")
    endif()

    execute_process(COMMAND \"${CMAKE_COMMAND}\" -S \"${CMAKE_SOURCE_DIR}\" -B \"\${SIM_DIR}\" -G Xcode
                    -DCMAKE_SYSTEM_NAME=Darwin -DCMAKE_OSX_SYSROOT=iphonesimulator -DCMAKE_OSX_ARCHITECTURES=arm64;x86_64
                    -DCMAKE_BUILD_TYPE=${EXT_APPLE_PACKAGE_CONFIG} -DEXT_IOS_AS_FRAMEWORK=${EXT_IOS_AS_FRAMEWORK}
                    -DEXT_TVOS_AS_FRAMEWORK=${EXT_TVOS_AS_FRAMEWORK} -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
                    -DEXT_APPLE_NATIVE_FRAMEWORK=${EXT_APPLE_NATIVE_FRAMEWORK}
                    RESULT_VARIABLE r3)
    if(NOT r3 EQUAL 0)
      message(FATAL_ERROR \"iOS simulator configure failed\")
    endif()
    execute_process(COMMAND \"${CMAKE_COMMAND}\" --build \"\${SIM_DIR}\" --config ${EXT_APPLE_PACKAGE_CONFIG} RESULT_VARIABLE r4)
    if(NOT r4 EQUAL 0)
      message(FATAL_ERROR \"iOS simulator build failed\")
    endif()

    # locate artifacts
    set(OUT_DIR \"${CMAKE_SOURCE_DIR}/out/ios_xcframework\")
    file(MAKE_DIRECTORY \"\${OUT_DIR}\")

    # Always remove the previous xcframework so xcodebuild can recreate cleanly
    file(REMOVE_RECURSE \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\")

    if(${EXT_IOS_AS_FRAMEWORK})
      set(DEV_ART \"\${DEVICE_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-iphoneos/${PROJECT_NAME}.framework\")
      set(SIM_ART \"\${SIM_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-iphonesimulator/${PROJECT_NAME}.framework\")
      execute_process(COMMAND xcodebuild -create-xcframework
                      -framework \"\${DEV_ART}\"
                      -framework \"\${SIM_ART}\"
                      -output \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\"
                      RESULT_VARIABLE r5)
      if(NOT r5 EQUAL 0)
        message(FATAL_ERROR \"xcframework create failed (framework mode)\")
      endif()
    else()
      # static-lib mode: need headers path if you want them embedded
      set(DEV_LIB \"\${DEVICE_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-iphoneos/lib${PROJECT_NAME}.a\")
      set(SIM_LIB \"\${SIM_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-iphonesimulator/lib${PROJECT_NAME}.a\")
      # heuristic public headers dir (adjust if you keep a different tree)
      set(HDRS_DIR \"\${CMAKE_BINARY_DIR}/_ios_headers\")
      file(REMOVE_RECURSE \"\${HDRS_DIR}\")
      file(MAKE_DIRECTORY \"\${HDRS_DIR}\")

      file(COPY
        \"${CMAKE_SOURCE_DIR}/src/ios/${PROJECT_NAME}_ios.h\"
        \"${CMAKE_SOURCE_DIR}/code_gen/ios/${PROJECT_NAME}Internal_ios.h\"
        DESTINATION \"\${HDRS_DIR}\"
      )
      execute_process(COMMAND xcodebuild -create-xcframework
                      -library \"\${DEV_LIB}\" -headers \"\${HDRS_DIR}\"
                      -library \"\${SIM_LIB}\" -headers \"\${HDRS_DIR}\"
                      -output \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\"
                      RESULT_VARIABLE r6)
      if(NOT r6 EQUAL 0)
        message(FATAL_ERROR \"xcframework create failed (static lib mode)\")
      endif()
    endif()

    # --- Zip the xcframework and drop it to the destination folder
    set(ZIP_OUT \"\${OUT_DIR}/${PROJECT_NAME}.zip\")
    # Make sure we start clean
    file(REMOVE \"\${ZIP_OUT}\")

    # Create the zip: put the xcframework folder inside the zip root
    execute_process(
      COMMAND \"\${CMAKE_COMMAND}\" -E tar \"cf\" \"\${ZIP_OUT}\" --format=zip \"${PROJECT_NAME}.xcframework\"
      WORKING_DIRECTORY \"\${OUT_DIR}\"
      RESULT_VARIABLE rz
    )
    if(NOT rz EQUAL 0)
      message(FATAL_ERROR \"Failed to create zip: \${ZIP_OUT}\")
    endif()

    # Ensure destination directory exists
    file(MAKE_DIRECTORY \"${IOS_DROP_DIR}\")

    # Overwrite existing file in destination
    file(REMOVE \"${IOS_DROP_DIR}/${PROJECT_NAME}.zip\")
    file(COPY \"\${ZIP_OUT}\" DESTINATION \"${IOS_DROP_DIR}\")
  ")

  # ---- tvOS xcframework target
  add_custom_target(
    package_tvos_xcframework
    COMMAND ${CMAKE_COMMAND} -E cmake_echo_color --cyan
            "Packaging tvOS xcframework (device + simulator)"
    COMMAND ${CMAKE_COMMAND} -DPLAT=tvOS -P
            "${CMAKE_BINARY_DIR}/_package_tvos.cmake"
    BYPRODUCTS
      "${CMAKE_BINARY_DIR}/../tvos_xcframework/${PROJECT_NAME}.xcframework"
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}"
    VERBATIM)

  file(
    WRITE "${CMAKE_CURRENT_BINARY_DIR}/_package_tvos.cmake"
    "
    set(DEVICE_DIR \"${CMAKE_BINARY_DIR}/_tvos_device\")
    set(SIM_DIR    \"${CMAKE_BINARY_DIR}/_tvos_sim\")
    file(REMOVE_RECURSE \"\${DEVICE_DIR}\" \"\${SIM_DIR}\")
    execute_process(COMMAND \"${CMAKE_COMMAND}\" -S \"${CMAKE_SOURCE_DIR}\" -B \"\${DEVICE_DIR}\" -G Xcode
                    -DCMAKE_SYSTEM_NAME=Darwin -DCMAKE_OSX_SYSROOT=appletvos -DCMAKE_OSX_ARCHITECTURES=arm64
                    -DCMAKE_BUILD_TYPE=${EXT_APPLE_PACKAGE_CONFIG} -DEXT_IOS_AS_FRAMEWORK=${EXT_IOS_AS_FRAMEWORK}
                    -DEXT_TVOS_AS_FRAMEWORK=${EXT_TVOS_AS_FRAMEWORK} -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
                    RESULT_VARIABLE r1)
    if(NOT r1 EQUAL 0)
      message(FATAL_ERROR \"tvOS device configure failed\")
    endif()
    execute_process(COMMAND \"${CMAKE_COMMAND}\" --build \"\${DEVICE_DIR}\" --config ${EXT_APPLE_PACKAGE_CONFIG} RESULT_VARIABLE r2)
    if(NOT r2 EQUAL 0)
      message(FATAL_ERROR \"tvOS device build failed\")
    endif()

    execute_process(COMMAND \"${CMAKE_COMMAND}\" -S \"${CMAKE_SOURCE_DIR}\" -B \"\${SIM_DIR}\" -G Xcode
                    -DCMAKE_SYSTEM_NAME=Darwin -DCMAKE_OSX_SYSROOT=appletvsimulator -DCMAKE_OSX_ARCHITECTURES=arm64;x86_64
                    -DCMAKE_BUILD_TYPE=${EXT_APPLE_PACKAGE_CONFIG} -DEXT_IOS_AS_FRAMEWORK=${EXT_IOS_AS_FRAMEWORK}
                    -DEXT_TVOS_AS_FRAMEWORK=${EXT_TVOS_AS_FRAMEWORK} -DEXT_USE_THIRD_PARTY=${EXT_USE_THIRD_PARTY}
                    RESULT_VARIABLE r3)
    if(NOT r3 EQUAL 0)
      message(FATAL_ERROR \"tvOS simulator configure failed\")
    endif()
    execute_process(COMMAND \"${CMAKE_COMMAND}\" --build \"\${SIM_DIR}\" --config ${EXT_APPLE_PACKAGE_CONFIG} RESULT_VARIABLE r4)
    if(NOT r4 EQUAL 0)
      message(FATAL_ERROR \"tvOS simulator build failed\")
    endif()

    # locate artifacts
    set(OUT_DIR \"${CMAKE_SOURCE_DIR}/out/tvos_xcframework\")
    file(MAKE_DIRECTORY \"\${OUT_DIR}\")

    # Always remove the previous xcframework so xcodebuild can recreate cleanly
    file(REMOVE_RECURSE \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\")

    if(${EXT_TVOS_AS_FRAMEWORK})
      set(DEV_ART \"\${DEVICE_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-appletvos/${PROJECT_NAME}.framework\")
      set(SIM_ART \"\${SIM_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-appletvsimulator/${PROJECT_NAME}.framework\")
      execute_process(COMMAND xcodebuild -create-xcframework
                      -framework \"\${DEV_ART}\"
                      -framework \"\${SIM_ART}\"
                      -output \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\"
                      RESULT_VARIABLE r5)
      if(NOT r5 EQUAL 0)
        message(FATAL_ERROR \"xcframework create failed (framework mode)\")
      endif()
    else()
      # static-lib mode: need headers path if you want them embedded
      set(DEV_LIB \"\${DEVICE_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-appletvos/lib${PROJECT_NAME}.a\")
      set(SIM_LIB \"\${SIM_DIR}/${EXT_APPLE_PACKAGE_CONFIG}-appletvsimulator/lib${PROJECT_NAME}.a\")
      # heuristic public headers dir (adjust if you keep a different tree)
      set(HDRS_DIR \"\${CMAKE_BINARY_DIR}/_tvos_headers\")
      file(REMOVE_RECURSE \"\${HDRS_DIR}\")
      file(MAKE_DIRECTORY \"\${HDRS_DIR}\")
      file(COPY
        \"${CMAKE_SOURCE_DIR}/src/tvos/${PROJECT_NAME}_tvos.h\"
        \"${CMAKE_SOURCE_DIR}/code_gen/tvos/${PROJECT_NAME}Internal_tvos.h\"
        DESTINATION \"\${HDRS_DIR}\"
      )
      execute_process(COMMAND xcodebuild -create-xcframework
                      -library \"\${DEV_LIB}\" -headers \"\${HDRS_DIR}\"
                      -library \"\${SIM_LIB}\" -headers \"\${HDRS_DIR}\"
                      -output \"\${OUT_DIR}/${PROJECT_NAME}.xcframework\"
                      RESULT_VARIABLE r6)
      if(NOT r6 EQUAL 0)
        message(FATAL_ERROR \"xcframework create failed (static lib mode)\")
      endif()
    endif()

    # --- Zip the xcframework and drop it to the destination folder
    set(ZIP_OUT \"\${OUT_DIR}/${PROJECT_NAME}.zip\")
    # Make sure we start clean
    file(REMOVE \"\${ZIP_OUT}\")

    # Create the zip: put the xcframework folder inside the zip root
    execute_process(
      COMMAND \"\${CMAKE_COMMAND}\" -E tar \"cf\" \"\${ZIP_OUT}\" --format=zip \"${PROJECT_NAME}.xcframework\"
      WORKING_DIRECTORY \"\${OUT_DIR}\"
      RESULT_VARIABLE rz
    )
    if(NOT rz EQUAL 0)
      message(FATAL_ERROR \"Failed to create zip: \${ZIP_OUT}\")
    endif()

    # Ensure destination directory exists
    file(MAKE_DIRECTORY \"${TVOS_DROP_DIR}\")

    # Overwrite existing file in destination
    file(REMOVE \"${TVOS_DROP_DIR}/${PROJECT_NAME}.zip\")
    file(COPY \"\${ZIP_OUT}\" DESTINATION \"${TVOS_DROP_DIR}\")
  ")
endif()
