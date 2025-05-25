# [东大节奏]

一款基于Unity开发的六轨道下落式音乐节奏游戏，灵感来源于Project SEKAI。

## 核心玩法

*   **操作方式**: 使用键盘上的 `S`, `D`, `F`, `J`, `K`, `L` 六个按键对应游戏中的六条轨道。
*   **游戏目标**: 当音符下落至判定线时，准确按下对应按键，获取高分和连击。
*   **音符类型**: 支持单击 (Tap) 和长按 (Hold) 音符。

## 主要特性

*   **谱面支持**: 使用 `.sus` 格式谱面，可通过在线编辑器 [PaletteWorks SUS Editor](https://paletteworks.mkpo.li/edit) 进行编写和编辑。
*   **内容**: 包含三个主要场景：选歌界面、游戏界面和结算界面。顺带一提，选歌界面的背景图是ai生成的东北大学校徽娘化。
*   **预设歌曲**: 内置三首示例歌曲：俊达派对之夜，KING，Override，其中第三首歌曲难度较高。
*   **动态背景**: 游戏背景会根据所选歌曲进行更换。
*   **判定与计分**: 包含 Perfect, Great, Good, Bad, Miss 五种判定，并有相应的计分和连击系统。

## 游戏演示

[选歌界面](Images/Select.png)
[游戏界面1](Images/俊达派对之夜.png)
[游戏界面2](Images/KING.png)
[游戏界面3](Images/Override.png)
[结算界面](Images/Results.png)

## 技术规格

*   **游戏引擎**: Unity 2023.2.20f1c1
*   **编程语言**: C#

## 如何开始

1.  **克隆仓库**:
    ```bash
    git clone https://github.com/33Sakito/-neu-rhythm-music-game-.git
    ```
2.  **打开项目**:
    *   使用 Unity Hub 打开克隆到本地的项目文件夹。
    *   Unity Editor 版本需为 2023.2.20f1c1 或兼容版本。
3.  **运行**:
    *   在Unity Editor中，打开 `Assets/Scenes/` 文件夹下的选歌场景。
    *   点击编辑器顶部的播放按钮即可开始。