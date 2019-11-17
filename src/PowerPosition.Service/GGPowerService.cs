using PowerPosition.Core;
using System.Diagnostics;
using System.ServiceProcess;

namespace PowerPosition.Service
{
    public partial class GGPowerService : ServiceBase
    {
        private readonly Extractor _extractor;
        
        public GGPowerService()
        {
            InitializeComponent();
            _extractor = new Extractor();
        }

        protected override void OnStart(string[] args)
        {            
            _extractor.Start();
        }

        protected override void OnStop()
        {
            _extractor.Stop();
        }

        protected override void OnShutdown()
        {
            _extractor.Stop();
        }

        protected override void OnPause()
        {
            base.OnPause();
            _extractor.Pause();
        }

        protected override void OnContinue()
        {
            base.OnContinue();
            _extractor.Continue();
        }
    }
}
