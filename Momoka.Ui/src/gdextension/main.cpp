#include <godot/gdextension_interface.h>
#include <godot/gdnative_interface.h>
#include <godot/classes/godot.hpp>
#include <godot/classes/ref_counted.hpp>

using namespace godot;

class MomokaUi : public RefCounted {
    GDCLASS(MomokaUi, RefCounted)

protected:
    static void _bind_methods() {}
};

extern "C" {

GDExtensionBool GDE_EXPORT momoka_ui_init(
    GDExtensionInterfaceGetProcAddress get_proc_addr,
    GDExtensionClassLibraryPtr library,
    GDExtensionInitialization *r_initialization)
{
    godot::GDExtensionBinding::InitOptions init_options;
    init_options.get_proc_address = get_proc_addr;
    init_options.library = library;
    init_options.initialization = r_initialization;
    init_options.level = GDEXTENSION_INITIALIZATION_CORE;

    auto binding = godot::GDExtensionBinding::create(init_options);
    return binding->init();
}

}
