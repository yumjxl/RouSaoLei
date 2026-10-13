# 再翻一张：UI 素材接入 v1

本次只改表现层，不改牌组比例、回合数、奖励或结算规则。

## 使用

Unity 2022.3 菜单：`Tools > 再翻一张 > 应用轻纸感界面`。
该操作在现有 MainUI 和 CardItem prefab 上应用配色，保留节点绑定与布局，并给卡牌添加图像层。
如果使用原有 `Rebuild Main UI Prefab` 重建，应再次应用本菜单。

## 素材

`Assets/AssetRaw/UIRaw/FlipAgain/FlipAgainIcons.png` 是透明 PNG，4 列 × 3 行，Unity 导入为 Multiple Sprite。
行序：基础 / 地雷 / 工具 / 收集；增幅 / 共鸣 / 倍频 / 测绘；回响 / 金币 / 护盾 / 牌背。
Sprite 名称与 CardConfig.Icon 对应。图案序列化到卡牌 prefab 中，随 prefab 依赖由 YooAsset 加载，不增加独立加载句柄。
文字、分数、角标不烘焙进图片。未翻牌不显示卡牌类型，侦察可显示类型但不新增数字信息。

## 制作说明

使用内置 imagegen 技能生成透明栅格美术，提示词要点：12 isolated icons, precise 4x3 grid, transparent background, warm charcoal hand-ink outlines, light pastel fills, no text or card frames。
使用本地 tengine-dev 技能约束资源位置、UI 生命周期及热更代码范围。

## 范围限制

后台启动 Unity 未成功，未产生导入或编译日志。本次尚未自动执行菜单、生成切片 meta 或验证 Game View；需要在编辑器内执行上述菜单并验证。

这是一套首版可接入素材与主界面表现层，不等于三个界面全部完成。
牌组预览、商店实际交互、金币经济及最终 Boss 本次没有新增。
正式发布前仍需检查不同分辨率、图标小尺寸清晰度、字体授权与实际运行效果。
