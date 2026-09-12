# 模組化命名規範

Reusable repository 預設使用 `<prefix>.<name>`。當多個 repository 長期屬於同一個穩定技術能力族時，可以使用：

```text
<prefix>.<family>.<name>
```

例如：

```text
module.verification.system-fact
module.verification.runtime-control
module.verification.oracle
```

對應 C# namespace 為 `Module.Verification.SystemFact`、`Module.Verification.RuntimeControl`、`Module.Verification.Oracle`。

Capability family 只代表 taxonomy 與 ownership grouping，不代表 integration、framework 或額外 dependency privilege。Family 內 module 仍遵守一般 `Module → Module` 規則，不得因此建立循環依賴。

Integration suffix 放在 capability name 之後。例如 `module.verification.diagnostics.unity3d` 對應 `Module.Verification.Diagnostics.Unity3D`，其中 `verification` 是 family、`diagnostics` 是 name、`unity3d` 是 integration。

Reusable repository 不得內嵌其他 shared repository 的 Git submodule；由最外層採用專案在 composition root 組合所需 capability。
