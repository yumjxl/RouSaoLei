---
name: html-to-ugui
description: HTML 原型转 Unity UGUI 智能 Prefab 生成管线。通过 AI 生成符合 UI-DSL 的 HTML，用 Playwright/浏览器烘焙 JSON v2 坐标、图片和适配意图，再导入 Unity 由 HtmlToUGUIBaker 生成可维护、多终端适配的 UGUI Prefab。触发场景：(1) 需要从自然语言生成 Unity UGUI 界面 (2) 需要从 HTML 原型烘焙 UGUI (3) UI 中包含图片并希望一键导入/绑定 Sprite (4) 需要 PC/mobile/pad 多终端适配 Prefab。
---

# HTML to UGUI 智能 Prefab 管线

你是 Unity UGUI 原型和 Prefab 生成专家。你的任务不是只做静态截图式原型，而是生成可被 `HtmlToUGUIBaker.cs` 消费的 UI-DSL HTML，并通过 JSON v2 把布局、图片、文本、控件和适配意图带回 Unity。

核心链路：

```text
自然语言需求 -> UI-DSL HTML -> Playwright/浏览器 JSON v2 -> Unity HtmlToUGUIBaker -> UGUI Prefab
```

## Step 1: 生成 UI-DSL HTML

先读取 [references/ui-dsl-spec.md](references/ui-dsl-spec.md)，再生成 HTML。

硬性规则：

- 唯一根节点必须声明 `data-u-type="div"`、`data-u-name="m_xxx"`，并有明确 `width/height`。
- 所有需要导入 Unity 的节点必须有 `data-u-name` 和 `data-u-type`。
- 允许类型：`div`、`image`、`text`、`button`、`input`、`scroll`、`toggle`、`slider`、`dropdown`。
- 图片节点优先使用 `data-u-type="image"`，并提供 `src` 或 `data-u-src`。
- 背景图片使用 CSS `background-image: url(...)`，必要时配合 `data-u-fit="cover|contain|stretch"`。
- 多终端适配要显式表达意图：`data-u-layout` 可使用 `stretch`、`top-bar`、`bottom-bar`、`left-panel`、`right-panel`、`center`、`fixed`。
- 需要安全区的全屏/边缘节点使用 `data-u-safe-area="true"`。

## Step 2: 烘焙 HTML -> JSON v2

推荐脚本：

```bash
python scripts/bake_html_to_json.py input.html -o output.json -w 1920 -H 1080
```

也可以打开 Unity 项目内的浏览器转换页：

```text
Assets/TEngine/Extension/HtmlToUGUI/HtmlToJson/HTML 转 JSON 坐标烘焙器.html
```

JSON v2 会保留 v1 字段，并额外输出：

- `schemaVersion`
- `designWidth` / `designHeight`
- `sourcePath` / `sourceDirectory`
- `imageSrc` / `backgroundImageSrc`
- `imageFit`
- `layoutHint`
- `safeArea`
- 可选 `anchorMin` / `anchorMax` / `pivot` / `offsetMin` / `offsetMax`

完整结构见 [references/json-schema.md](references/json-schema.md)。

## Step 3: 导入 Unity 生成 Prefab

1. 打开 `Tools > UI Architecture > HTML to UGUI Baker (Full Controls)`。
2. 分配 `HtmlToUGUIConfig` 和目标 Canvas。
3. 在配置中确认图片导入目录，默认推荐 `Assets/AssetRaw/UIRaw/Raw/HtmlToUGUI`。
4. 选择 JSON 文件或粘贴 JSON 字符串。
5. 点击“执行烘焙生成”。

Unity baker 会：

- 对 v1 JSON 使用固定左上坐标，保持兼容。
- 对 v2 JSON 优先应用显式 anchor/offset，再用 `layoutHint`，再用几何启发式，最后回退固定坐标。
- 解析 `<img src>`、`data-u-src`、CSS `background-image`。
- 对 `Assets/`、`Packages/` 路径直接加载 Sprite。
- 对本地相对路径按 HTML/JSON 目录解析，并复制导入到配置的 Unity 目录。
- 图片缺失时继续生成 Prefab 并输出 warning。

控件映射见 [references/control-mapping.md](references/control-mapping.md)。

## 快速参考

### 分辨率预设

| 预设 | 尺寸 |
| --- | --- |
| PC 横屏 | 1920x1080 |
| Mobile 竖屏 | 1080x1920 |
| Pad 横屏 | 2048x1536 |

### 图片写法

```html
<img data-u-type="image" data-u-name="m_imgAvatar"
     src="images/avatar.png"
     data-u-layout="fixed"
     data-u-fit="contain"
     style="width: 96px; height: 96px;">

<div data-u-type="image" data-u-name="m_imgBg"
     data-u-layout="stretch"
     data-u-safe-area="true"
     style="width: 1920px; height: 1080px; background-image: url(images/bg.png); background-size: cover;">
</div>
```

### 常用适配 hint

| hint | Unity 语义 |
| --- | --- |
| `stretch` | 四边拉伸，适合全屏背景/遮罩 |
| `top-bar` | 横向拉伸并贴顶部 |
| `bottom-bar` | 横向拉伸并贴底部 |
| `left-panel` | 纵向拉伸并贴左侧 |
| `right-panel` | 纵向拉伸并贴右侧 |
| `center` | 居中固定尺寸，适合弹窗 |
| `fixed` | 固定左上坐标，兼容 v1 |

## 典型场景

用户说：“做一个带头像、背景图、底部导航的手游主界面。”

处理方式：

1. 根节点按目标分辨率写满屏。
2. 背景图节点使用 `data-u-layout="stretch"` 和 `background-image`。
3. 顶部栏用 `data-u-layout="top-bar"`。
4. 底部导航用 `data-u-layout="bottom-bar"`。
5. 头像用 `<img data-u-type="image" src="...">`。
6. 运行 bake 脚本输出 JSON v2。
7. 在 Unity baker 中一键生成带 Sprite 的自适应 UGUI Prefab。
