using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class Program
{
    static async Task Main()
    {
        // 1. Tìm file Calculator.cs đi ngược lên từ thư mục build (bin)
        string? calculatorPath = FindUpwardFile(AppContext.BaseDirectory, "Calculator.cs");
        if (calculatorPath == null)
        {
            Console.WriteLine("Lỗi: Không tìm thấy file Calculator.cs!");
            return;
        }

        Console.WriteLine($"[1] Đã tìm thấy file tại: {calculatorPath}");

        // 2. Đọc toàn bộ nội dung file code Calculator.cs
        string methodCode = await File.ReadAllTextAsync(calculatorPath, Encoding.UTF8);

        // 3. Chuẩn bị câu lệnh chi tiết (Prompt) gửi cho AI
        var prompt = $"""
        Write a complete, valid C# xUnit test class for the following Calculator class.
        Make sure to import necessary namespaces (e.g., 'Xunit' and 'DemoUnitTest_ConsoleApp').
        Write test cases for all available methods.
        Use ```csharp and ``` to wrap the code.

        Source Code to test:
        {methodCode}
        """;

        // 4. Cấu hình HttpClient để gọi API tới LM Studio
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "lm-studio");

        // 5. Tạo body request chứa cả SYSTEM PROMPT để định hướng AI
        var body = new
        {
            model = "openai/gpt-oss-20b", // LM Studio sẽ tự động map với model đang chạy của bạn
            messages = new[]
            { 
                // Ép AI đóng vai chuyên gia viết code, cấm nói chuyện dông dài
                new {
                    role = "system",
                    content = "You are a C# Unit Testing expert. Your task is to output ONLY valid C# xUnit test code wrapped inside a ```csharp ... ``` code block. Do NOT write any introduction, explanations, or conversational filler. Start directly with the code."
                },
                new {
                    role = "user",
                    content = prompt
                }
            },
            max_tokens = 1000, // Tăng giới hạn từ 400 lên 1000 để tránh bị cắt cụt code giữa chừng
            stream = false,
            temperature = 0.1  // Giảm temperature xuống thấp để AI viết code chuẩn xác, ít sáng tạo linh tinh
        };

        // Chuyển đối tượng body thành chuỗi JSON
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Console.WriteLine("[2] Đang gửi request tới LM Studio (vui lòng đợi AI sinh code)...");

        try
        {
            // 6. Gửi POST Request tới server LM Studio cục bộ
            var resp = await client.PostAsync("http://localhost:1234/v1/chat/completions", content);
            resp.EnsureSuccessStatusCode();

            // 7. Đọc và phân tách dữ liệu JSON
            var text = await resp.Content.ReadAsStringAsync();
            var raw = JObject.Parse(text)["choices"]![0]!["message"]!["content"]!.ToString();

            Console.WriteLine($"\n--- DỮ LIỆU THÔ TỪ AI TRẢ VỀ ---\n{raw}\n--------------------------------\n");

            // Xóa các ký tự bọc markdown
            string unitTestCode = StripCodeFence(raw);

            if (string.IsNullOrWhiteSpace(unitTestCode) || unitTestCode == raw && !raw.Contains("class"))
            {
                Console.WriteLine("Cảnh báo: Dữ liệu trả về có vẻ không chứa mã nguồn C# hợp lệ!");
            }

            // 8. Định vị thư mục dự án UnitTest và lưu file
            var unitTestDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(calculatorPath)!, "UnitTest"));
            Directory.CreateDirectory(unitTestDir);

            string outFile = Path.Combine(unitTestDir, "UnitTest_Generated.cs");
            await File.WriteAllTextAsync(outFile, unitTestCode, Encoding.UTF8);

            Console.WriteLine($"[3] Thành công! Đã lưu file test tại: {outFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Có lỗi xảy ra: {ex.Message}");
        }
    }

    // Hàm bổ trợ: Đi ngược từ thư mục hiện tại lên các thư mục cha để tìm đúng file
    static string? FindUpwardFile(string start, string name, int max = 8)
    {
        var d = new DirectoryInfo(start);
        for (int i = 0; i < max && d != null; i++, d = d.Parent)
        {
            string c = Path.Combine(d.FullName, name);
            if (File.Exists(c)) return c;
        }
        return null;
    }

    // Hàm bổ trợ: Cắt lấy phần code nằm giữa ```csharp và ``` một cách an toàn nhất
    static string StripCodeFence(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;

        if (s.Contains("```"))
        {
            int start = s.IndexOf("```");
            // Tìm dấu đóng ``` sau dấu mở đầu tiên ít nhất 3 ký tự
            int end = s.LastIndexOf("```");

            if (end > start)
            {
                s = s.Substring(start + 3, end - start - 3);
            }
        }

        // Làm sạch các định danh ngôn ngữ thừa ở dòng đầu tiên
        if (s.StartsWith("csharp")) s = s.Substring(6);
        if (s.StartsWith("cs")) s = s.Substring(2);

        return s.Trim();
    }
}
