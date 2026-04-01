# PS4 (ORBIS) post-project hook: code_gen source discovery, includes, defines
# Detected via: CMAKE_SYSTEM_NAME = "ORBIS"

file(GLOB SRC_CORE CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/core/*.cpp
)
file(GLOB SRC_COMMON CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/*.cpp
)
file(GLOB_RECURSE SRC_NATIVE CONFIGURE_DEPENDS
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.[cS]
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/native/*.cpp
)

target_sources(${PROJECT_NAME} PRIVATE ${SRC_CORE} ${SRC_COMMON} ${SRC_NATIVE})
target_include_directories(${PROJECT_NAME} PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen
  ${CMAKE_CURRENT_SOURCE_DIR}/code_gen/core
)
target_compile_definitions(${PROJECT_NAME} PRIVATE OS_PS4 EXTGEN_HAS_JNI=0)

# TODO: Add PS4-specific SDK link libraries and include directories.

message(STATUS "Including PS4 sources")
