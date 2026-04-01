# PS5 (Prospero) post-build: copy output binary to the extension directory
#
# TODO: Determine the correct output artifact (.prx / .elf) and copy to ${_EXT_OUT_DIR}.
# Example (adjust extension as needed):
#
# add_custom_command(
#   TARGET ${PROJECT_NAME} POST_BUILD
#   COMMAND "${CMAKE_COMMAND}" -E copy_if_different
#           "$<TARGET_FILE:${PROJECT_NAME}>"
#           "${_EXT_OUT_DIR}/$<TARGET_FILE_NAME:${PROJECT_NAME}>"
#   COMMENT "Copying PS5 binary to ${_EXT_OUT_DIR}"
# )
