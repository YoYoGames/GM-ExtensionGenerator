set(SRC_DIR ${CMAKE_CURRENT_LIST_DIR})

# =============================================================================
# WINDOWS
# =============================================================================
if(WIN32)
  file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp")
  # =============================================================================
  # APPLE (macOS, iOS, tvOS)
  # =============================================================================
elseif(APPLE)
  # =============================================================================
  # iOS
  # =============================================================================
  if(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/ios/*.mm")
    # =============================================================================
    # tvOS
    # =============================================================================
  elseif(CMAKE_SYSTEM_NAME STREQUAL "tvOS")
    file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/tvos/*.mm")
    # =============================================================================
    # macOS
    # =============================================================================
  else()
    file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp")
  endif()
  # =============================================================================
  # ANDROID
  # =============================================================================
elseif(ANDROID)
    file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/android/*.cpp")
  # =============================================================================
  # LINUX
  # =============================================================================
elseif(UNIX)
  file(GLOB TARGET_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp")
endif()

target_sources(${PROJECT_NAME} PRIVATE ${TARGET_SRCS})
target_include_directories(${PROJECT_NAME} PRIVATE "${SRC_DIR}")
