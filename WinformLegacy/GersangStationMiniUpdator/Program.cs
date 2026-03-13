namespace GersangStationMiniUpdator;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (!UpdateArguments.TryParse(args, out UpdateArguments? options, out string errorMessage))
        {
            MessageBox.Show(
                $"{errorMessage}{Environment.NewLine}{Environment.NewLine}{UpdateArguments.BuildUsage()}",
                "거상 스테이션 미니 업데이트",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        Application.Run(new Form1(options!));
    }    
}
