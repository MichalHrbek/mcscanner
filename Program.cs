using System;
using System.IO;
using mcscanner;

string inpath = @"ips";
string outpath = @"results";
int threadCount = 10;
int waitms = 500;

byte[] ips = File.ReadAllBytes(inpath);
var sp = new ServerPing(new HostList(ips), threadCount, waitms);
using (StreamWriter writer = new StreamWriter(outpath))
{
	while (!sp.done)
	{
        Console.WriteLine($"Succes: {sp.successful}; Error: {sp.unsuccessful}; Total: {sp.successful + sp.unsuccessful} / {ips.Length / 6}; {6f*(sp.successful+sp.unsuccessful)/(ips.Length):P2}");
        while (sp.output.Count != 0)
        {
            writer.WriteLine(sp.output.Dequeue());
        }
        Thread.Sleep(1000);
    }
}