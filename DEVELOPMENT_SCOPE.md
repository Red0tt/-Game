# 当前开发的最小范围

入口：`Assets/Level/try/FanhuaMechanismLab.unity`。

修翻花时只从以下文件开始，不遍历整个工程、不读取整份场景 YAML：
- `Assets/Scripts/PrototypeFanhua.cs`：格点、长度、材料身份、接触、推拉、起点、二维投影。
- `Assets/Scripts/FanhuaPaperMesh.cs`：连续纸带与刺的网格。
- `Assets/Scripts/PrototypePlayer.cs`：实际方向输入与形态切换。
- `Assets/Scripts/Unit02WaterSimulation.cs`：有限水量、排水、反应和灰石。
- `Assets/Scripts/PrototypeWaterLevel.cs`：水地形与连续水面显示。
- `Assets/Scripts/FanhuaMechanismLab.cs`：专项实验按钮。

测试入口：`Assets/Editor/ValidateFanhuaMechanismLab.cs`；水与灰石回归：`ValidateGrayPlaytest.cs`、`GrayRuleChecks.cs`。场景需要重建才查看 `BuildFanhuaMechanismLab.cs`。其他关卡、庭院和旧实验场景先不修改。

规则最新补充位于 `PROJECT_PLAN.md` 第14节；历史记录不必每轮全部读取。

## Git 最小交付

Unity 必需源文件为 Assets（含每个资源的 .meta）、Packages（含 lock 文件）、ProjectSettings。规划文档与 Tools 中的只读检查脚本一起管理。Library、Temp、Logs、IDE工程文件、构建、Backups、ArtReview 与测试输出不提交；Unity 会自行重建缓存。

检查：`python3 Tools/check_unity_sync.py`，再运行 `git diff --check`。检查覆盖丢失 .meta、重复 GUID、场景资源引用和 Build Settings 场景路径。输出成功只代表本地待提交源文件完整；完成 commit/push 后，其他机器才能接收版本。

已有场景保留，避免删除可游玩内容。场景移动必须保留原 .meta GUID，Git 应识别成重命名，不能只提交旧路径删除而漏掉新路径。
