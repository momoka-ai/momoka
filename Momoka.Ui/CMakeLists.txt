cmake_minimum_required(VERSION 3.20)
project(momoka_ui_extension LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# vcpkg toolchain
if(DEFINED ENV{VCPKG_ROOT})
    set(CMAKE_TOOLCHAIN_FILE "$ENV{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake" CACHE STRING "")
endif()

# Godot CPP (GDExtension)
find_package(godot-cpp CONFIG REQUIRED)

# GDExtension source
add_library(momoka_ui SHARED
    src/gdextension/main.cpp
)

target_include_directories(momoka_ui PRIVATE src)
target_link_libraries(momoka_ui PRIVATE godot::cpp)

# Install
install(TARGETS momoka_ui LIBRARY DESTINATION lib)
