
# 🚀 **Testify: Test Cases Automation Tool**

A **full-stack automated API testing platform** powered by ⚡ AI + 🧪 real-time validation.
Generate test cases, execute APIs, validate responses, and manage everything inside a clean & interactive UI.

[Frontend Repo Link](https://github.com/kunals495/Test-Cases-Automation-Frontend/blob/main/src/components/ValidationPage.tsx)

<p align="center">
  <img src="https://img.shields.io/badge/Backend-.NET%206%2F7-blue?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/Frontend-React%20%2B%20TypeScript-green?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/AI-Google%20GenAI-orange?style=for-the-badge"/>
</p>

---

## 📚 **Table of Contents**

* [✨ Features](#-features)
* [🛠 Tech Stack](#-tech-stack)
* [📦 Prerequisites](#-prerequisites)
* [⚙️ Installation](#️-installation)
* [🧑‍💻 Usage](#-usage)
* [🖼 Screenshots](#-screenshots)
* [🎥 Demo Video](#-demo-video)
* [📡 API Documentation](#-api-documentation)
* [📁 Project Structure](#-project-structure)
* [🤖 AI Test Generation](#-ai-test-generation)
* [👥 Contributing](#-contributing)
* [📜 License](#-license)
* [💬 Support](#-support)

---

## ✨ **Features**

### 🎯 **Core Functionality**

* 🤖 **AI-Powered Test Case Generation**
* 📊 **Excel Import/Export for Test Templates**
* ⚡ **Real-time API Testing**
* 🧠 **Smart Response Validation**
* 📝 **Add, Edit, Delete Test Cases with Ease**
* 💾 **Persistent Storage (LocalStorage)**
* 📡 **Live SSE Progress Updates**

### 🎨 **User Experience**

* 🎈 Clean, modern UI
* 🎚 Collapsible test details
* 🔔 Toast notifications
* 🖥 Real-time colored results
* 📉 Statistics Dashboard

---

## 🖼 **Screenshots**

| Screenshot                                                   | Description                  |
| ------------------------------------------------------------ | ---------------------------- |
| ![Main Dashboard](https://github.com/user-attachments/assets/7ee5d131-bf41-49bc-aa82-90f886f4cc6d)            | 📌 Main Test Dashboard       |
| ![Generate Template](https://github.com/user-attachments/assets/674f4a03-b374-4b98-87af-333745d39e99) | 📝 Generate Excel Template   |
| ![Upload Progress](https://github.com/user-attachments/assets/a650a1dd-19c5-4c72-982f-22b510bdea70)     | ⏳ Upload + Live Progress     |
| ![Test Details](https://github.com/user-attachments/assets/2dbbfa31-03ca-42a7-b8bd-662427e0b9cd)           | 🔍 Expanded Test Details     |
| ![Filter Stats](https://github.com/user-attachments/assets/c246bdc2-5917-4148-af52-d62625a5c54a)           | 📊 Add Test Cases |

---


## 🎥 **Demo Video**

[![Demo Video](./screenshots/video-thumbnail.png)](https://github.com/user-attachments/assets/1e181171-2d72-4a58-be21-e67bc5aab3fd)

---

## 🛠 **Tech Stack**

### 🖥 Frontend

* React (TS)
* React Router
* React Toastify
* CSS3

### ⚙ Backend

* ASP.NET Core 6/7
* EPPlus
* Google GenAI
* Newtonsoft.Json

---

## 📦 **Prerequisites**

* 📌 Node.js 16+
* 📌 npm or yarn
* 📌 .NET SDK 6+
* 📌 VS Code or Visual Studio

---

## ⚙️ **Installation**

### 🖥 Frontend Setup

```bash
git clone <repository-url>
cd test-cases-automation/client
npm install
npm start
```

App runs on **[http://localhost:3000](http://localhost:3000)**

### ⚙ Backend Setup

```bash
cd server
dotnet restore
dotnet run
```

API runs on **[https://localhost:7242](https://localhost:7242)**

---

## 🔐 **AI Configuration**

Inside `TestController.cs`:

```csharp
var aiService = new CopilotAIService("YOUR_GOOGLE_AI_API_KEY");
```

---

## 🧑‍💻 **Usage**

### 1️⃣ **Generate Excel Template**

👉 Enter API base URL
👉 AI analyzes endpoints
👉 Excel downloaded automatically

### 2️⃣ **Upload Excel & Execute Tests**

* Upload Excel
* Tests run automatically via SSE
* Live progress tracking

### 3️⃣ **Manual Test Management**

* ➕ Add new test cases
* ✏️ Edit inline
* ❌ Delete

### 4️⃣ **Filter Test Results**

* 🔵 All
* 🟢 Pass
* 🔴 Fail

### 5️⃣ **Re-run Tests**

Click **Validate** to retry pending/failed tests.

---

## 📡 **API Documentation**

### 🔹 Execute Single API Test

```http
POST /api/test/execute-api
```

### 🔹 Run Tests with SSE

```http
POST /api/test/run-test-live
```

### 🔹 Download Results

```http
GET /api/test/download-result/{fileId}
```

### 🔹 Generate Test Cases

```http
POST /api/test/generate-testcases
```

---

## 📁 **Project Structure**

```
test-cases-automation/
├── client/        # React App
├── server/        # ASP.NET API
└── README.md
```

---

## 🤖 **AI Test Generation Logic**

* Analyzes method + params
* Creates 10+ scenarios:

  * 👍 Positive cases
  * 👎 Negative cases
  * 🔄 Edge cases
* Auto-suggests payload + expected response

---

## 👥 **Contributing**

PRs are welcome!
Steps:

1. Fork repo
2. Create feature branch
3. Commit changes
4. Push
5. Create PR 🚀

---

## 📜 **License**

Licensed under **MIT**.

---

Built with ❤️ for automated testing excellence by Kunal Singh
