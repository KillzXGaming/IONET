﻿using System;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using IONET;

namespace ColladaTest
{
    class Program
    {


        static void Main(string[] args)
        {
            var scene = IOManager.LoadScene("untitled.glb", new ImportSettings()
            {

            });
            IOManager.ExportScene(scene, "untitledRB.glb", new ExportSettings()
            {

            });

            //  Console.WriteLine("Mesh Count: " + scene.Models[0].Meshes.Count);
        }
    }
}