# STS2MCP 中文说明

[English README](./README.md)

## 项目简介

本项目是一个用于 **Slay the Spire 2** 的 Mod，目标是让外部 AI 程序能够读取游戏状态并执行操作。

仓库当前包含两部分核心能力：

- `STS2_MCP.dll`：游戏内 Mod，负责把状态和动作通过本地 HTTP API 暴露出来
- `orchestrator/`：Python 编写的宠物伴侣与自动化控制器，负责建议模式、自动模式、状态同步和宠物气泡展示

这个仓库是基于原始项目 [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP) 继续开发的 fork。原项目提供了基础 MCP bridge 与 API 设计；本 fork 在其基础上扩展了宠物伴侣界面、Python orchestrator、建议模式、自动模式，以及一系列状态同步和稳定性修复。

## 这个项目能做什么

- 通过本地 REST API 读取 STS2 当前游戏状态
- 通过 API 执行出牌、用药、选图、选奖励、事件选择等操作
- 通过 MCP 方式连接 Claude Desktop / Claude Code 等支持 MCP 的客户端
- 使用宠物伴侣界面显示建议、模式状态和自动化反馈
- 使用 Python orchestrator 轮询状态并驱动 `pause / advise / auto` 三种模式

## 目录说明

- `McpMod*.cs`：C# Mod 主体逻辑
- `mcp/server.py`：MCP 服务端
- `orchestrator/`：宠物伴侣与自动化 orchestrator
- `tests/STS2_MCP.Tests/`：C# 单元测试
- `orchestrator/tests/`：Python 测试

## 安装与使用

### 1. 安装游戏 Mod

前置要求：

- 已安装 **Slay the Spire 2**
- 已安装 `.NET 9 SDK`

构建 Mod：

```powershell
.\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
```

或者先设置环境变量：

```powershell
$env:STS2_GAME_DIR = "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
.\build.ps1
```

构建完成后，会生成：

```text
out/STS2_MCP/STS2_MCP.dll
mod_manifest.json
```

把它们复制到游戏目录的 `mods/` 下：

```text
out/STS2_MCP/STS2_MCP.dll  -> <game_install>/mods/STS2_MCP.dll
mod_manifest.json          -> <game_install>/mods/STS2_MCP.json
```

启动游戏后，在设置里启用 Mod。启用后，游戏会在本地启动 HTTP 服务，默认地址是：

```text
http://127.0.0.1:15526
```

### 2. 连接 MCP

前置要求：

- Python 3.11+
- `uv`

进入仓库后，可以把 MCP server 配到 Claude Code 或 Claude Desktop。

示例配置：

```json
{
  "mcpServers": {
    "sts2": {
      "command": "uv",
      "args": ["run", "--directory", "/path/to/STS2MCP/mcp", "python", "server.py"]
    }
  }
}
```

完整工具说明见：

- [mcp/README.md](./mcp/README.md)
- [docs/raw_api.md](./docs/raw_api.md)

### 3. 使用宠物伴侣 orchestrator

进入 `orchestrator/` 目录，先复制配置模板：

```powershell
cd .\orchestrator
Copy-Item .\sts2_pet.toml.example .\sts2_pet.toml
```

然后安装：

```powershell
C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe -m pip install -e .
```

#### OpenAI 兼容网关示例

```toml
provider_name = "openai_compatible"
poll_interval_seconds = 0.75
timeout_seconds = 90

game_base_url = "http://127.0.0.1:15526"
pet_base_url = "http://127.0.0.1:15526"

[provider]
api_key = "your-key"
base_url = "https://your-gateway.example/v1"
model = "gpt-4.1-mini"
```

#### 本地 Codex CLI 示例

```toml
provider_name = "codex_cli"
poll_interval_seconds = 0.75
timeout_seconds = 90

[provider]
codex_cmd = "codex.cmd"
codex_model = "gpt-5-codex"
```

#### 本地 Claude Code CLI 示例

```toml
provider_name = "claude_cli"
poll_interval_seconds = 0.75
timeout_seconds = 90

[provider]
claude_cmd = "claude"
claude_model = "claude-sonnet-4-6"
```

启动方式：

```powershell
cd .\orchestrator
C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe -m sts2_pet.cli --mode advise
C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe -m sts2_pet.cli --mode auto
C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe -m sts2_pet.cli --mode advise --once
```

项目里也提供了快捷脚本：

- `orchestrator/start-pet-advise.cmd`
- `orchestrator/start-pet-auto.cmd`

### 4. 模式说明

- `pause`：不轮询，不调用模型
- `advise`：读取当前状态，生成建议并显示宠物气泡
- `auto`：读取状态，生成单步动作计划，并尝试自动执行

## 开发与测试

### C# 测试

```powershell
dotnet test .\tests\STS2_MCP.Tests\STS2_MCP.Tests.csproj
```

### Python 测试

```powershell
C:\Users\colezhang\AppData\Local\Programs\Python\Python312\python.exe -m pytest .\orchestrator\tests -q
```

## 许可证与致谢

本项目沿用上游仓库的 MIT 许可证。发布、修改和分发时，请保留原始许可证与来源说明。

- 原始项目：[Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)
- 当前 fork：在原项目基础上扩展了宠物伴侣、orchestrator 和自动化工作流
