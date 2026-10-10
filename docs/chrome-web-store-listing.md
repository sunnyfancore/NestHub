# Chrome Web Store 上架填写资料

对应插件：NestHub 快捷收藏 1.1.1。以下资料依据当前扩展代码编写，适用于自部署 NestHub 版本。

## 商品详情

### 标题（由软件包读取）

NestHub 快捷收藏

### 摘要（由软件包读取）

将当前网页快捷添加到自己的 NestHub 导航空间。

### 说明（复制到「说明」）

NestHub 快捷收藏是开源书签导航工具 NestHub 的浏览器扩展。连接你自行部署或有权使用的 NestHub 服务，即可将正在浏览的网页保存到自己的导航空间。

主要功能

• 自动填入当前网页标题和地址，可在保存前修改。
• 选择所属分类，添加描述、标签或置顶设置。
• 记住最近使用的分类，减少重复操作。
• 可选网页贴边收藏按钮：主动开启后显示，可拖动到左右边缘并记住位置，也可隐藏或关闭。
• 支持工具栏入口及可自行绑定的快捷键。
• 使用 API 令牌连接，不需要在扩展中输入邮箱或密码。

开始使用

1. 准备可用的 NestHub 服务及至少一个分类。本扩展不提供统一云端托管服务。
2. 在 NestHub 后台「API 调用说明」中复制 API 令牌。
3. 点击扩展图标，填写服务根地址和令牌，允许访问该服务并验证连接。
4. 打开要收藏的网页，确认标题、地址及分类，点击「添加到 NestHub」，等待成功提示。

贴边按钮与快捷键

贴边按钮默认关闭。如需使用，在扩展中勾选「在网页上显示贴边收藏按钮」，并允许访问网页。Chrome 内部页、扩展商店等受限制页面无法显示按钮。

默认建议快捷键为 Alt+Shift+B，浏览器可能因冲突未自动绑定。请在 chrome://extensions/shortcuts 中检查或修改绑定。

隐私与连接说明

API 令牌及显示偏好保存在扩展本地存储中。收藏数据仅在用户确认保存时发送到用户配置的 NestHub 服务；扩展不包含广告或第三方统计工具，不自动上传完整浏览记录。具体的数据处理说明请查看隐私政策。

远程服务须使用 HTTPS；本机开发支持 localhost。当前服务端令牌为有有效期的 JWT，失效后需更换。暂不支持部署在子路径下的 NestHub 服务。

项目及部署文档：https://github.com/sunnyfancore/NestHub

### 类别与语言

- 类别：选择「生产力」或分类菜单中最接近的「工具」/「工作流程与规划」。菜单名称可能变化，以当前可选项为准。
- 语言：中文（简体）。
- 官方网站/主页：https://github.com/sunnyfancore/NestHub
- 支持网址：https://github.com/sunnyfancore/NestHub/issues
- 宣传视频：没有则留空。
- Google Analytics：留空；当前扩展未接入统计。

## 隐私权

### 单一用途说明

帮助用户将主动选择的网页按分类保存到其配置的 NestHub 服务。工具栏、快捷键和可选贴边按钮都是同一收藏功能的入口。

### activeTab 权限理由

用户通过工具栏或快捷键主动打开扩展时，读取当前标签页的 URL 和标题，自动填入收藏表单。仅用于用户发起的网页收藏，不读取网页正文或完整浏览历史。

### storage 权限理由

在扩展本地存储中保存用户配置的 NestHub 服务地址、API 令牌、最近分类以及浮窗开关和位置，便于恢复连接和显示偏好。API 令牌仅供扩展受信任上下文访问，不提供给网页浮窗脚本，不使用云端同步存储。

### scripting 权限理由

仅在用户主动开启并授权贴边收藏功能后，将扩展包内的 floating.js 注入允许的网页，显示可移动的收藏入口。该脚本不读取网页正文或表单数据，不收集键盘输入，用户可随时关闭浮窗。

### 主机访问权限理由

NestHub 支持用户自行部署，服务域名无法预先限定。连接时只申请用户输入的服务域名权限，用于带令牌读取分类和保存收藏。只有用户主动开启贴边按钮时，才另外请求 HTTP/HTTPS 网页访问权限，以在浏览的网页上显示收藏入口。未开启浮窗也可使用工具栏收藏；权限不用于广告、跟踪或自动采集浏览记录。

### 是否使用远程代码

选择「否」。所有可执行脚本均包含在扩展包内。NestHub API 仅返回业务数据，扩展不下载或执行远程脚本，不使用远程代码或 eval。

### 隐私政策网址

https://github.com/sunnyfancore/NestHub/blob/main/docs/extension-privacy-policy.md

**此网址必须在政策文件提交到公开 GitHub 仓库后才可填写使用。请先在未登录窗口确认能够访问。** 不可用本地文件链接、草稿链接代替公开网址。

### 数据类型勾选建议

基于当前版本，勾选「身份验证信息」（API 令牌）、「网页记录/网络历史记录」（用户选择的当前网页 URL 和标题）及「网站内容」（页面标题及用户确认保存的收藏内容）。即使不采集完整浏览历史，也不要用「不收集任何数据」覆盖实际处理和传输的数据。具体名称以隐私表单为准。

不要勾选健康、财务、位置、个人通信等与现有功能无关的数据。扩展不收集邮箱、密码或个人资料，不记录点击日志、键盘输入或浏览轨迹；浮窗位置是界面偏好，不是地理位置。

数据使用认证项如与以下表述一致，可据当前代码勾选：

- 不向第三方出售或转移用户数据（用户指定的 NestHub 服务为完成核心功能所必需的接收方）。
- 不将数据用于与单一用途无关的目的。
- 不将数据用于信用评估或借贷决策。

若表单出现更具体或不同的选项，应按其定义核对，不要直接全选。

## 分发

- 价格：免费（若界面提供此项）。
- 可见性：公开；如仅先测试，可选择不公开/通过链接访问，仍需遵守审核要求。
- 国家/地区：按你希望服务的区域选择；没有地域限制可选择全部可用地区。
- 交易者身份：按真实商业用途填写，不由插件是否免费决定。

## 测试说明（复制到审核测试说明）

This extension is a companion to the open-source, self-hosted NestHub bookmark manager. It does not provide a shared hosted service or require an account with the extension developer.

Source code and deployment instructions: https://github.com/sunnyfancore/NestHub

Testing the bookmark workflow requires a working NestHub instance. Follow the repository README to deploy NestHub locally with .NET 10, Node.js and MySQL, configure your own administrator credentials, and open http://localhost:5186. Create at least one category, then copy the Bearer JWT from the administration page titled “API 调用说明”.

Open the extension, enter http://localhost:5186 and the copied token, and approve access to the local instance. Open https://example.com in another tab, open the extension, select a category and click “添加到 NestHub”. Verify the created bookmark in the local NestHub instance.

The optional floating button can be tested without a NestHub connection: open the extension, enable “在网页上显示贴边收藏按钮”, approve HTTP/HTTPS access, and open or refresh https://example.com. A movable collection button appears at the screen edge. Clicking it opens an extension window with the source page URL and title. Drag the button to change its position; click × to hide it on the current page; disable the setting in the extension to turn it off globally.

The toolbar and shortcut workflows are available without enabling the floating button. Configure the shortcut at chrome://extensions/shortcuts if necessary. Browser-internal pages and the Chrome Web Store restrict script injection.

All code is bundled. The extension does not ask for an email or password and does not execute remote code. The API token is kept in extension-local storage with access restricted to trusted extension contexts. Remote NestHub instances require HTTPS; local development supports HTTP loopback addresses.

### 当前审核条件

没有公开测试服务或测试账号，不要编造凭据。本地部署说明可能不足以满足审核人员的测试需要；如被要求补充，准备独立的低权限测试空间及可撤销、有效期足够的测试令牌，仅通过私有审核表单提供。不要把真实 API 令牌写入公开仓库、截图或商品详情。

## 图片上传

- 商店图标：使用 `extensions/store-assets/icon-128.png`，128×128。
- 截图：至少 1 张，最多 5 张，1280×800 或 640×400，JPEG 或无透明通道的 PNG。请拍摄实际运行界面，不要用效果图冒充操作截图。
- 推荐截图 1：普通网页旁展示已连接的收藏窗口，显示标题、地址和分类；不要出现令牌。
- 推荐截图 2：网页边缘显示收藏按钮以及展开的收藏窗口。
- 推荐截图 3：NestHub 中已保存的链接，说明收藏结果。
- 可选小型宣传图块：使用 `extensions/store-assets/promo-440x280.png`。
- 宣传视频和其他可选宣传图可留空。

## 提交顺序

1. 将隐私政策文件发布到公开 GitHub main 分支并验证可访问。
2. 下载或重新生成 1.1.1 插件包，在「文件包」页上传，保留现有商店项目 ID，不另建项目。
3. 填写商品详情并上传图标、至少一张真实截图。
4. 填写隐私权、权限理由、政策网址和测试说明。
5. 完成账号与分发所需信息，保存草稿，点击「我为何无法提交？」确认剩余项。
6. 确认资料和测试条件后提交审核。

上述文件已在本地准备；未自动上传仓库、填写商店或提交审核。
