# GM-ExtensionGenerator
Repository for GameMaker's Extension Generator tool.

**Schema-driven code generator for GameMaker native extensions**

extgen is a **code generation tool** that produces **GML bindings**, **native platform glue**, and **CMake build systems** for GameMaker extensions from a single schema-based input.

It is designed to be:
- ✅ **Target-driven** (platforms decide what gets generated)
- ✅ **Deterministic & repeatable**
- ✅ **Schema-validated**
- ✅ **Extensible and contributor-friendly**

---

## ✨ What extgen does

From one GMIDL input file, extgen can generate:

- GameMaker **GML bindings**
- **C++ native glue code**
- Android (Java / Kotlin / JNI)
- iOS & tvOS (ObjC / Swift / Native)
- Consoles (Xbox, PS4, PS5, Switch)
- Complete **CMake projects & presets**
- Optional documentation output

All behavior is controlled via a **JSON config file** validated by an automatically generated **JSON Schema**.

---

## 🚀 Quick Start

```bash
# Initialize a new project
extgen --init ./my-extension

# Generate everything
extgen --config ./my-extension/config.json
````

---

## 📚 Documentation

All documentation lives in the **GitHub Wiki**:

* **User Manual** — configuration, targets, workflows
* **Build Guide** — how to build extgen from source
* **Developer Docs** — architecture, emitters, contribution guide

👉 **Start here:**
[**`Wiki → Home`**](../../wiki)

---

## 🧱 Requirements

* **.NET 9 SDK**
* Windows, macOS, or Linux

---

## 🧪 Status

* Actively developed
* Production-ready core
* Open to contributions

---

## 🤝 Contributing

Contributions are welcome!

If you want to add:

* new targets
* new emitters
* improvements or fixes

Please read the **Developer Documentation** in the wiki before submitting a PR.

---

## 📄 License

See `LICENSE` for details.

