set(SRC_DIR ${CMAKE_CURRENT_LIST_DIR})

# =============================================================================
# WINDOWS
# =============================================================================
if(WIN32)
  file(GLOB GMWEBVIEW_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp"
       "${SRC_DIR}/GMWebViewManager/common/*.cpp"
       "${SRC_DIR}/GMWebViewManager/windows/*.cpp")
  # =============================================================================
  # APPLE (macOS, iOS, tvOS)
  # =============================================================================
elseif(APPLE)
  # =============================================================================
  # iOS
  # =============================================================================
  if(CMAKE_SYSTEM_NAME STREQUAL "iOS")
    file(GLOB GMWEBVIEW_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/ios/*.mm"
         "${SRC_DIR}/GMWebViewManager/ios/*.cpp")
    # =============================================================================
    # tvOS
    # =============================================================================
  elseif(CMAKE_SYSTEM_NAME STREQUAL "tvOS")
    # =============================================================================
    # macOS
    # =============================================================================
  else()
    file(GLOB GMWEBVIEW_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp"
         "${SRC_DIR}/GMWebViewManager/macos/*.cpp")
  endif()
  # =============================================================================
  # ANDROID
  # =============================================================================
elseif(ANDROID)
  # =============================================================================
  # LINUX
  # =============================================================================
elseif(UNIX)
  file(GLOB GMWEBVIEW_SRCS CONFIGURE_DEPENDS "${SRC_DIR}/native/*.cpp"
       "${SRC_DIR}/GMWebViewManager/common/*.cpp"
       "${SRC_DIR}/GMWebViewManager/linux/*.cpp")
endif()

target_sources(${PROJECT_NAME} PRIVATE ${GMWEBVIEW_SRCS})
target_include_directories(${PROJECT_NAME}
                           PRIVATE "${SRC_DIR}" "${SRC_DIR}/GMWebViewManager")
