using System;
using System.IO;
using mcscanner;

string inpath = @"C:\Users\hppcp\Downloads\mcscanner\ips";
string outpath = @"C:\Users\hppcp\Downloads\mcscanner\results";
int threadCount = 10;
int waitms = 500;

byte[] ips = File.ReadAllBytes(inpath);
var sp = new ServerPing(new HostList(ips), threadCount, waitms);
using (StreamWriter writer = new StreamWriter(outpath))
{
	while (!sp.done)
	{
        Console.WriteLine($"Succes: {sp.successful}; Error: {sp.successful}; Total: {sp.successful + sp.successful} / {ips.Length / 6}");
        while (sp.output.Count !=0)
        {
            writer.WriteLine(sp.output.Dequeue());
        }
        Thread.Sleep(1000);
    }
}