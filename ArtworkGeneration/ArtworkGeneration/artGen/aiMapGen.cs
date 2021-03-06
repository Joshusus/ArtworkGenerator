﻿using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//todo change references from salvage
using Windows.UI;
using Windows.Foundation;
using Windows.Storage;
using ArtworkGeneration.artGen;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace ArtworkGeneration.artGen
{
    public class aiMapGen
    {
        /**
         
         Terminology:
            - image: A Color[,] object
            - map: A Color[,] object, where only the R and A (opacity) values are being used. By using only R, we are working with a 2D object, like a B&W image.
            
        */



        static Color defaultImageClr;
        static ICanvasResourceCreator draw = null;

        static aiMapGen()
        {
            defaultImageClr = Color.FromArgb(255, 128, 0, 0);
            draw = new CanvasDevice();
        }

        //Public image methods
        public async static void GenDemoBitmap(CanvasDrawingSession draw)
        {
            /** Example code of how to generate a bitmap purely from code (ie. not from loading an existing image)
             * Also shows how to save a bitmap to file.
             */

            if (draw == null) return;

            # region Generate a 64x64 bitmap

            // - Generate a 2d array of Color objects 
            int width = 64;
            int height = 64;
            Color[,] byteMap = new Color[width, height];

            // - Work with this 2d array in code
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) byteMap[x, y] = Color.FromArgb(255, Convert.ToByte((((float)y / (float)height) * 100) + (((float)x / (float)width) * 100)), 0, 0);

            // - Convert this to a bitmap

            //Convert to 1d array
            Color[] outpByteMap = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) outpByteMap[(y * height) + x] = byteMap[x, y];

            CanvasBitmap bmp = CanvasBitmap.CreateFromColors(draw, outpByteMap, width, height);

            #endregion

            #region save to file

            string filename = "demo_image.png";
            StorageFolder pictureFolder = KnownFolders.SavedPictures;
            var file = await pictureFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await bmp.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }

            bool done = true;

            #endregion

        }
        public static void CreatePaintPourImage(MapSettings session)
        {

            //RNG
            int tmp = (session.Y + ((session.X + 1) / 2));
            var seedHash = Helper.debug_MapGenSeed;
            var randomHelper = new cRandomHelper(seedHash);

            //Image bounds - generates a map 3x the size, then crops to the centre third.
            int width = cSystemGen.totalCanvasTileSize * 3;
            int height = cSystemGen.totalCanvasTileSize * 3;

            int minDrawX = cSystemGen.totalCanvasTileSize;
            int maxDrawX = cSystemGen.totalCanvasTileSize * 2;
            int minDrawY = cSystemGen.totalCanvasTileSize;
            int maxDrawY = cSystemGen.totalCanvasTileSize * 2;
            int drawWidth = cSystemGen.totalCanvasTileSize;
            int drawHeight = cSystemGen.totalCanvasTileSize;

            //heatmap
            Color[,] map = matrixPlain(width, height, Color.FromArgb(Convert.ToByte(255), Convert.ToByte(0), Convert.ToByte(0), Convert.ToByte(0)));

            // - Image heatmap formation
            #region standard image generation
            //Density - no. of gaussians dropped in
            double imageDensity = randomHelper.RandomDouble(0.1, 1.2);
            if (imageDensity < 0.7) imageDensity *= 0.5;

            double[,] kernel;
            int i = 0;
            bool addingGaussians = true;
            int numGaussians = (int)(imageDensity * 65);
            while (addingGaussians)
            {
                var sz = randomHelper.RandomNumber(3, drawWidth);
                var opacity = randomHelper.RandomNumber(10, 255);
                opacity = (int)Math.Round((float)opacity * (float)sz / (float)drawWidth);

                var gaussian = matrixGaussian(sz, Color.FromArgb(Convert.ToByte(opacity), 255, 0, 0));

                var leftX = randomHelper.RandomNumber(minDrawX - (int)(sz / 2f), maxDrawX - (int)(sz / 2f));
                var topY = randomHelper.RandomNumber(minDrawY - (int)(sz / 2f), maxDrawY - (int)(sz / 2f));

                addMask(map, gaussian, 0.5, false, leftX, topY);
                i++;
                if (i > numGaussians) addingGaussians = false;
            }

            var setKernelSize = 5;
            kernel = kernelPlain(setKernelSize, 1f / (setKernelSize * setKernelSize));
            map = conv2DMask(map, kernel);
            #endregion          

            #region add image features
            //May be useful for other image features (Target => (Point) where empty point is. Score => (double) radius of empty point.)
            List<PriorityObject> emptyPoints = new List<PriorityObject>();

            // - Mesa cluster
            bool hasMesas = randomHelper.RandomDouble(0, 1) < 0.65;
            if (hasMesas)
            {
                var mesaMap = GenMesaTrailMap(width, height, randomHelper, emptyPoints);
                paintMask(map, mesaMap);
            }

            #endregion

            #region setup neigbor tiles for seamless knitting

            // - Get section of image for current tile
            map = matrixCropped(drawWidth, drawHeight, minDrawX, minDrawY, map);
            width = drawWidth;
            height = drawHeight;
            session.thisHeatmap = map;

            //Neigbor tiles
            /*
            system.gen.tlHeatmap = matrixCropped(drawWidth, drawHeight, 0, 0, bigMap);
            system.gen.tHeatmap = matrixCropped(drawWidth, drawHeight, minDrawX, 0, bigMap);
            system.gen.trHeatmap = matrixCropped(drawWidth, drawHeight, maxDrawX, 0, bigMap);

            system.gen.lHeatmap = matrixCropped(drawWidth, drawHeight, 0, minDrawY, bigMap);
            system.gen.rHeatmap = matrixCropped(drawWidth, drawHeight, maxDrawX, minDrawY, bigMap);

            system.gen.blHeatmap = matrixCropped(drawWidth, drawHeight, 0, maxDrawY, bigMap);
            system.gen.bHeatmap = matrixCropped(drawWidth, drawHeight, minDrawX, maxDrawY, bigMap);
            system.gen.brHeatmap = matrixCropped(drawWidth, drawHeight, maxDrawX, maxDrawY, bigMap);
            */

            #endregion

            // - Set values in system object
            SetSessionVals(width, height, map, session, emptyPoints);

            saveAsCanvasBitmap(new CanvasDevice(), session.outputMap, width, height, "OUTPUT_IMG");

        }
        
        static void SetSessionVals(int width, int height, Color[,] map, MapSettings session, List<PriorityObject> newMesaCentres)
        {
            /** Generates output image(s) & saves output image(s) to session */

            // - Painting / generating images
            var totalClrMap = blueSeaColorLookupCombo(map);

            // - Set values in system object
            session.outputMap = totalClrMap;
        }

        //Image features
        static Color[,] GenMesaMap(int width, int height, ICanvasResourceCreator draw, cRandomHelper randomHelper)
        {
            /** Generates a map filled with mesas. */

            //Mesas
            Color[,] mesaMap = matrixPlain(width, height, Color.FromArgb(255, 0, 0, 0));

            List<PriorityObject> mesaCentrePts = new List<PriorityObject>();

            // - Large gaussians. define where mesas will be
            var i = 0;
            var addingGaussians = true;
            double avgMesaSize = 40;
            int attempt = 0;
            while (addingGaussians)
            {
                var sz = randomHelper.RandomNumber((int)Math.Round(avgMesaSize * 0.7), (int)avgMesaSize);

                // top left coordinates
                var x = randomHelper.RandomNumber(0 - (sz / 2), width - 1 - (sz / 2));
                var y = randomHelper.RandomNumber(0 - (sz / 2), height - 1 - (sz / 2));

                var cx = x + (sz / 2);
                var cy = y + (sz / 2);


                //check pixel is being added to, only add if v low or v high value
                if (cx >= 0 && cx < width && cy >= 0 && cy < width)
                {
                    Point pt = new Point(cx, cy);

                    //Check against existing mesas
                    bool valid = true;
                    foreach (var po in mesaCentrePts)
                    {
                        var gap = ((sz / 2) + (po.score)) + 5;
                        if (Helper.GetDistance(pt, (Point)po.target) < gap)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        var gaussian = matrixCircle(sz, Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 128, 0, 0));
                        addMask(mesaMap, gaussian, 2, false, x, y);
                        i++;
                        attempt = 0;
                        if (i > 300) addingGaussians = false;

                        PriorityObject po = new PriorityObject(pt, sz / 2f);
                        mesaCentrePts.Add(po);
                    }
                    else //Try a different mesa at different spot
                    {
                        attempt += 1;
                        if (attempt > 60) addingGaussians = false;
                    }
                }
            }

            saveAsCanvasBitmap(draw, mesaMap, width, height, "jjImage_Mesas_0");

            //Fill out Mesas
            i = 0;
            addingGaussians = true;
            attempt = 0;

            Dictionary<PriorityObject, List<PriorityObject>> mesaSupportPts = new Dictionary<PriorityObject, List<PriorityObject>>();
            foreach (PriorityObject centre in mesaCentrePts) mesaSupportPts.Add(centre, new List<PriorityObject>()); //TODO add centre pt to support pts

            while (addingGaussians)
            {

                PriorityObject po = mesaCentrePts[randomHelper.RandomNumber(0, mesaCentrePts.Count - 1)];
                var mesaRadius = po.score;
                double heading = randomHelper.RandomHeading();
                double dist = randomHelper.RandomDouble(mesaRadius * 0.4, mesaRadius * 1.0); //0.8
                var sz = randomHelper.RandomNumber((int)Math.Round(mesaRadius * 0.5), (int)Math.Round(mesaRadius * 2.8));

                //Check this area will be empty
                var relPt = Helper.GetRelativePoint((Point)po.target, heading, dist);

                bool valid = dist + sz > mesaRadius;

                var cx = (int)relPt.X;
                var cy = (int)relPt.Y;

                if (valid)
                {
                    valid = true;
                    foreach (var centrePo in mesaCentrePts)
                    {
                        if (centrePo == po) continue;
                        //Check vs centre blob
                        var reqGap = ((sz / 2) + (centrePo.score)) + 5;
                        var d = Helper.GetDistance(relPt, (Point)centrePo.target);
                        if (d < reqGap)
                        {
                            valid = false;
                            break;
                        }
                        else
                        {
                            //Check vs support blobs
                            foreach (var supPo in mesaSupportPts[centrePo])
                            {
                                reqGap = ((sz / 2) + (supPo.score)) + 5;
                                d = Helper.GetDistance(relPt, (Point)supPo.target);
                                if (d < reqGap)
                                {
                                    valid = false;
                                    break;
                                }
                            }
                        }
                    }

                }

                if (valid)
                {
                    //Add
                    i++;
                    attempt = 0;
                    if (i > 300) addingGaussians = false;
                    mesaSupportPts[po].Add(new PriorityObject(relPt, sz / 2f));
                }
                else
                {
                    attempt += 1;
                    if (attempt == 200) mesaRadius *= 0.8;
                    else if (attempt == 250) mesaRadius *= 0.8;
                    else if (attempt == 300) mesaRadius *= 0.8;
                    else if (attempt > 400) addingGaussians = false;
                }

            }

            //Create blob images and add
            mesaMap = matrixPlain(width, height, Color.FromArgb(255, 0, 0, 0));
            i = 0;
            foreach (var centrePo in mesaCentrePts)
            {
                var blobMap = matrixPlain(width, height, Color.FromArgb(255, 128, 0, 0));
                Point pt = (Point)centrePo.target;
                var r = centrePo.score;
                var cx = pt.X - r;
                var cy = pt.Y - r;

                var gaussian = matrixCircle((int)r * 2, Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 128, 0, 0));
                addMask(blobMap, gaussian, 2, false, (float)cx, (float)cy);

                var supPts = mesaSupportPts[centrePo];
                foreach (var supPo in supPts)
                {

                    pt = (Point)supPo.target;
                    r = supPo.score;
                    cx = pt.X - r;
                    cy = pt.Y - r;

                    gaussian = matrixCircle((int)r * 2, Color.FromArgb(255, 255, 0, 0), Color.FromArgb(255, 128, 0, 0));
                    addMask(blobMap, gaussian, 2, false, (float)cx, (float)cy);
                }

                //Blur 
                var setKernelSize = 3;
                var kernel = kernelPlain(setKernelSize, 1f / (setKernelSize * setKernelSize));
                blobMap = conv2DMask(blobMap, kernel);
                addMask(mesaMap, blobMap, 2);
                i++;

            }

            saveAsCanvasBitmap(draw, mesaMap, width, height, "jjImage_Mesas_2");
            return mesaMap;
        }
        static Color[,] GenMesaTrailMap(int width, int height, cRandomHelper randomHelper, List<PriorityObject> mesaCentres, MesaSettings settings = null)
        {

            /** Generates a map with a mesa trail inside. Mesas should not continue outside the boundaries of the image. */

            //Use these boundaries for drawing
            int minDrawX = cSystemGen.totalCanvasTileSize;
            int maxDrawX = cSystemGen.totalCanvasTileSize * 2;
            int minDrawY = cSystemGen.totalCanvasTileSize;
            int maxDrawY = cSystemGen.totalCanvasTileSize * 2;
            int drawWidth = cSystemGen.totalCanvasTileSize;
            int drawHeight = cSystemGen.totalCanvasTileSize;

            //Mesas
            Color[,] mesaMap = matrixPlain(width, height, Color.FromArgb(0, 0, 0, 0));
            Color[,] mesaBackgroundMap = matrixPlain(width, height, Color.FromArgb(0, 0, 0, 0));
            List<PriorityObject> mesaCentrePts = mesaCentres;
            if (mesaCentres == null) mesaCentrePts = new List<PriorityObject>(); //mesacentres was null
            List<PriorityObject> addedMesaCentrePts = new List<PriorityObject>();

            #region setup magic numbers
            //Global
            int maxMesas = 250;
            double mesaAreaCoverOpacity = 0.85;

            //Spawn brush
            Point brushPt = new Point(randomHelper.RandomNumber((int)(minDrawX + (drawWidth * 1f / 3f)), (int)(maxDrawX - (drawWidth * 1f / 3f))), randomHelper.RandomNumber((int)(minDrawY + (drawHeight * 1f / 3f)), (int)(maxDrawY - (drawHeight * 1f / 3f))));
            int brushMesaRadius = randomHelper.RandomNumber((int)(drawWidth * 0.03), (int)(drawWidth * 0.08)); //20,60
            var brushVel = Helper.GetRelativePoint(0, 0, randomHelper.RandomHeading(), randomHelper.RandomNumber((int)(brushMesaRadius * 0.3), (int)(brushMesaRadius * 0.8)));
            int brushSteps = randomHelper.RandomNumber(10, 25);
            int brushCurrStep = 0;
            double brushRadiusChangeVel = randomHelper.RandomDouble(-0.2, 0.2);
            int brushAvgMesaSize = randomHelper.RandomNumber(33, 38); //35, 45

            //Background shading
            float gaussMul = 2;
            float gaussAdd = 20;

            if (settings != null)
            {
                if (settings.maxMesas.HasValue) maxMesas = settings.maxMesas.Value;
                if (settings.mesaAreaCoverOpacity.HasValue) mesaAreaCoverOpacity = settings.mesaAreaCoverOpacity.Value;


                if (settings.gaussMul.HasValue) gaussMul = settings.gaussMul.Value;
                if (settings.gaussAdd.HasValue) gaussAdd = settings.gaussAdd.Value;

                if (settings.brushPt.HasValue) brushPt = settings.brushPt.Value;
                if (settings.brushMesaRadius.HasValue) brushMesaRadius = settings.brushMesaRadius.Value;
                if (settings.brushVel.HasValue) brushVel = settings.brushVel.Value;
                if (settings.brushSteps.HasValue) brushSteps = settings.brushSteps.Value;
                if (settings.brushCurrStep.HasValue) brushCurrStep = settings.brushCurrStep.Value;
                if (settings.brushRadiusChangeVel.HasValue) brushRadiusChangeVel = settings.brushRadiusChangeVel.Value;
                if (settings.brushAvgMesaSize.HasValue) brushAvgMesaSize = settings.brushAvgMesaSize.Value;
            }

            #endregion

            #region Create mesa trail using a brush
            while (brushCurrStep < brushSteps)
            {

                //Fill area with mesas
                fillAreaMesaCentres(ref mesaMap, brushPt, brushMesaRadius, brushAvgMesaSize, randomHelper, mesaCentrePts, addedMesaCentrePts, settings);

                var gaussian = matrixGaussian((int)((brushMesaRadius * 2 * gaussMul) + (2 * gaussAdd)), Color.FromArgb(255, 180, 0, 0));
                paintMask(mesaBackgroundMap, gaussian, 2, false, (float)brushPt.X - (brushMesaRadius * gaussMul) - gaussAdd, (float)brushPt.Y - (brushMesaRadius * gaussMul) - gaussAdd);

                //Update brush
                brushCurrStep++;
                if (mesaCentrePts.Count > maxMesas) brushCurrStep = brushSteps;

                brushPt.X += brushVel.X;
                brushPt.Y += brushVel.Y;

                double headingChangeHeading = randomHelper.RandomDouble(0, 2 * Math.PI);
                double headingChangeVel = randomHelper.RandomDouble(0, brushMesaRadius * 0.2);
                Point velChange;
                if (brushPt.X < (minDrawX + (drawWidth * 0.25)) || brushPt.X > (maxDrawX - (drawWidth * 0.25)) || brushPt.Y < (minDrawY + (drawHeight * 0.25)) || brushPt.Y > (maxDrawY - (drawHeight * 0.25)))
                {
                    velChange = Helper.GetRelativePoint(0, 0, Helper.GetHeadingTowardsPoint(brushPt.X, brushPt.Y, width * 0.5, height * 0.5) + randomHelper.RandomDouble(-1.2, 1.2), 100);
                }
                else velChange = Helper.GetRelativePoint(0, 0, headingChangeHeading, headingChangeVel);


                brushVel.X = brushVel.X + velChange.X;
                brushVel.Y = brushVel.Y + velChange.Y;
                //Cap velocity of brush
                double totalVel = Math.Sqrt(Math.Pow(brushVel.X, 2) + Math.Pow(brushVel.Y, 2));
                if (totalVel > brushMesaRadius * 0.8)
                {
                    var mul = (brushMesaRadius * 0.8) / totalVel;
                    brushVel.X *= mul;
                    brushVel.Y *= mul;
                }

                brushRadiusChangeVel = Math.Max(-0.3, Math.Min(0.3, brushRadiusChangeVel + randomHelper.RandomDouble(-0.1, 0.1)));
                brushMesaRadius = (int)Math.Round((1f + brushRadiusChangeVel) * brushMesaRadius);
                if (brushMesaRadius > drawWidth * 0.3) { brushMesaRadius = (int)(drawWidth * 0.3); brushRadiusChangeVel = 0; }

                brushAvgMesaSize = (int)Math.Round(randomHelper.RandomDouble(0.80, 1.05) * brushAvgMesaSize);
                brushAvgMesaSize = Math.Max(6, Math.Min(60, brushAvgMesaSize)); //5

            }

            if (addedMesaCentrePts.Count > 0) fillOutMesas(ref mesaMap, brushPt, brushMesaRadius, randomHelper, addedMesaCentrePts, settings);

            #endregion

            reduceOpacity(ref mesaBackgroundMap, mesaAreaCoverOpacity); //0.85
            paintMask(mesaBackgroundMap, mesaMap);

            mesaCentres = mesaCentrePts;
            return mesaBackgroundMap;
        }

        //Image feature submethods
        static Color[,] fillAreaMesaCentres(ref Color[,] mesaMap, Point brushPt, int brushRadius, int avgMesaSize, cRandomHelper randomHelper, List<PriorityObject> mesaCentrePts, List<PriorityObject> newMesaCentrePts, MesaSettings settings = null)
        {
            /** Fills an area with mesa centres. A mesa centre is a circle, and can be turned into a full 'mesa' by filling it out with more circles or other shapes.
             * Mesas will NOT intersect. */

            var width = mesaMap.GetUpperBound(0) + 1;
            var height = mesaMap.GetUpperBound(1) + 1;

            double lowerBoundCentreSizeMul = 0.7;
            if (settings != null && settings.lowerBoundCentreSizeMul.HasValue) lowerBoundCentreSizeMul = settings.lowerBoundCentreSizeMul.Value;


            // - Large gaussians. define where mesas will be
            #region define mesa centres
            var i = 0;
            var addingGaussians = true;
            //double avgMesaSize = 40;
            int attempt = 0;
            while (addingGaussians)
            {
                var sz = randomHelper.RandomNumber((int)Math.Round(avgMesaSize * lowerBoundCentreSizeMul), (int)avgMesaSize);

                if (settings != null && settings.firstCentreIsAvgSize.HasValue && settings.firstCentreIsAvgSize.Value) sz = avgMesaSize;

                Point pt = Helper.GetRelativePoint(brushPt, randomHelper.RandomHeading(), randomHelper.RandomNumber(1, brushRadius));


                //check pixel is being added to, only add if v low or v high value
                if (pt.X >= 0 && pt.X < width && pt.Y >= 0 && pt.Y < width)
                {
                    //Check against existing mesas
                    bool valid = true;
                    foreach (var po in mesaCentrePts)
                    {
                        var gap = ((sz / 2) + (po.score)) + 5;
                        if (Helper.GetDistance(pt, (Point)po.target) < gap)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        var x = (float)pt.X - (sz / 2);
                        var y = (float)pt.Y - (sz / 2);

                        var gaussian = matrixCircle(sz, Color.FromArgb(255, 255, 0, 0), Color.FromArgb(0, 0, 0, 0));
                        paintMask(mesaMap, gaussian, 2, false, x, y);
                        i++;
                        attempt = 0;
                        if (i > 300) addingGaussians = false;

                        PriorityObject po = new PriorityObject(pt, sz / 2f);
                        mesaCentrePts.Add(po);
                        newMesaCentrePts.Add(po);
                    }
                    else //Try a different mesa at different spot
                    {
                        attempt += 1;
                        if (attempt > 60) addingGaussians = false;
                    }
                }
            }
            #endregion

            return mesaMap;
        }
        static Color[,] fillOutMesas(ref Color[,] mesaMap, Point brushPt, int brushRadius, cRandomHelper randomHelper, List<PriorityObject> mesaCentrePts, MesaSettings settings = null)
        {
            /** Fills out mesa centres by adding more circles to each mesa randomly.
             * Will continue to make sure mesas do NOT intersect. */

            #region magic numbers
            var width = mesaMap.GetUpperBound(0) + 1;
            var height = mesaMap.GetUpperBound(1) + 1;

            var totalBlobsToAdd = mesaCentrePts.Count * 10;
            var blurKernelSize = 5; //3
            var lowerBoundMesaSize = 0.5;
            var upperBoundMesaSize = 2.8;

            double mesaBlobMul = 2;

            if (settings != null)
            {
                if (settings.totalBlobsToAdd.HasValue) totalBlobsToAdd = settings.totalBlobsToAdd.Value;
                else if (settings.totalBlobsToAddMul.HasValue) totalBlobsToAdd = mesaCentrePts.Count * settings.totalBlobsToAddMul.Value;
                if (settings.blurKernelSize.HasValue) blurKernelSize = settings.blurKernelSize.Value;
                if (settings.lowerBoundFOMesaSize.HasValue) lowerBoundMesaSize = settings.lowerBoundFOMesaSize.Value;
                if (settings.upperBoundFOMesaSize.HasValue) upperBoundMesaSize = settings.upperBoundFOMesaSize.Value;
                if (settings.mesaBlobMul.HasValue) mesaBlobMul = settings.mesaBlobMul.Value;
            }

            #endregion

            #region fill out mesas
            //Fill out Mesas
            var i = 0;
            var addingGaussians = true;
            var attempt = 0;

            Dictionary<PriorityObject, List<PriorityObject>> mesaSupportPts = new Dictionary<PriorityObject, List<PriorityObject>>();
            foreach (PriorityObject centre in mesaCentrePts) mesaSupportPts.Add(centre, new List<PriorityObject> { centre });

            while (addingGaussians)
            {

                PriorityObject po = mesaCentrePts[randomHelper.RandomNumber(0, mesaCentrePts.Count - 1)];
                var mesaRadius = po.score;
                double heading = randomHelper.RandomHeading();
                double dist = randomHelper.RandomDouble(mesaRadius * 0.4, mesaRadius * 1.0); //0.8
                var sz = randomHelper.RandomNumber((int)Math.Round(mesaRadius * lowerBoundMesaSize), (int)Math.Round(mesaRadius * upperBoundMesaSize));

                //Check this area will be empty
                var relPt = Helper.GetRelativePoint((Point)po.target, heading, dist);

                bool valid = dist + sz > mesaRadius;

                var cx = (int)relPt.X;
                var cy = (int)relPt.Y;

                if (valid)
                {
                    valid = true;
                    foreach (var centrePo in mesaCentrePts)
                    {
                        if (centrePo == po) continue;
                        else
                        {
                            //Check vs support blobs
                            foreach (var supPo in mesaSupportPts[centrePo])
                            {
                                var reqGap = ((sz / 2) + (supPo.score)) + 5;
                                var d = Helper.GetDistance(relPt, (Point)supPo.target);
                                if (d < reqGap)
                                {
                                    valid = false;
                                    break;
                                }
                            }
                        }
                    }

                }

                if (valid)
                {
                    //Add
                    i++;
                    attempt = 0;
                    if (i > totalBlobsToAdd) addingGaussians = false; //300
                    mesaSupportPts[po].Add(new PriorityObject(relPt, sz / 2f));
                }
                else
                {
                    attempt += 1;
                    if (attempt == 200) mesaRadius *= 0.8;
                    else if (attempt == 250) mesaRadius *= 0.8;
                    else if (attempt == 300) mesaRadius *= 0.8;
                    else if (attempt > 400) addingGaussians = false;
                }

            }
            #endregion

            #region generate mesa images
            //Create blob images and add
            mesaMap = matrixPlain(width, height, Color.FromArgb(0, 0, 0, 0));
            i = 0;
            foreach (var centrePo in mesaCentrePts)
            {

                //Work out minimum size blobmap needs to be - improves performance
                int minX = width;
                int minY = height;
                int maxX = 0;
                int maxY = 0;

                var supPts = mesaSupportPts[centrePo];
                foreach (var supPo in supPts)
                {
                    var pt = (Point)supPo.target;
                    var r = supPo.score;

                    var leftX = pt.X - r;
                    var rightX = pt.X + r;
                    var topY = pt.Y - r;
                    var bottomY = pt.Y + r;

                    minX = (int)Math.Min(minX, leftX);
                    maxX = (int)Math.Max(maxX, rightX) + 1;
                    minY = (int)Math.Min(minY, topY);
                    maxY = (int)Math.Max(maxY, bottomY) + 1;
                }

                int setWidth = maxX - minX;
                int setHeight = maxY - minY;

                var blobMap = matrixPlain(setWidth, setHeight, Color.FromArgb(0, 0, 0, 0));
                //var supPts = mesaSupportPts[centrePo];
                foreach (var supPo in supPts)
                {

                    var pt = (Point)supPo.target;
                    var r = supPo.score;
                    var leftX = pt.X - r;
                    var topY = pt.Y - r;

                    var bmLeftX = leftX - minX;
                    var bmTopY = topY - minY;

                    var gaussian = matrixCircle((int)r * 2, Color.FromArgb(255, 255, 0, 0), Color.FromArgb(0, 0, 0, 0));
                    paintMask(blobMap, gaussian, 2, false, (float)bmLeftX, (float)bmTopY);
                }



                //Blur 
                var kernel = kernelPlain(blurKernelSize, 1f / (blurKernelSize * blurKernelSize));
                blobMap = conv2DMaskColorOpacity(blobMap, kernel); //expensive

                //saveAsCanvasBitmap(new CanvasDevice(), blobMap, width, height, "testy");

                //Add
                paintMask(mesaMap, blobMap, mesaBlobMul, false, minX, minY);
                i++;

            }
            #endregion

            return mesaMap;
        }
               
        //Matrix Constructors
        static Color[,] matrixPlain(int width, int height, Color? setClr = null)
        {
            /** Constructs a Color[,] 'image' matrix: Plain colour. */
            Color setClrVal = defaultImageClr;
            if (setClr.HasValue) setClrVal = setClr.Value;

            Color[,] byteMap = new Color[width, height];

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) byteMap[x, y] = Color.FromArgb(setClrVal.A, setClrVal.R, setClrVal.G, setClrVal.B);

            return byteMap;
        }
        static Color[,] matrixGradient(int width, int height)
        {
            /** Constructs a Color[,] 'image' matrix: Colour gradient from topleft to bottomright */
            Color[,] byteMap = new Color[width, height];

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) byteMap[x, y] = Color.FromArgb(255, Convert.ToByte((((float)y / (float)height) * 100) + (((float)x / (float)width) * 100)), 0, 0);

            return byteMap;
        }
        static Color[,] matrixNoise(int width, int height, cRandomHelper randomHelper, int range, Color? avgClr = null)
        {
            /** Constructs a Color[,] 'image' matrix: Noise 
             Uses an even distribution within range */

            Color setClr = defaultImageClr;
            if (avgClr.HasValue) setClr = avgClr.Value;

            Color[,] byteMap = new Color[width, height];

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var setVal = randomHelper.RandomNumber(setClr.R - range, setClr.R + range);
                    if (setVal < 0) setVal = 0;
                    if (setVal > 255) setVal = 255;
                    byteMap[x, y] = Color.FromArgb(setClr.A, Convert.ToByte(setVal), 0, 0);
                }

            return byteMap;
        }
        static Color[,] matrixGaussian(int size, Color? centreClr = null, double mulA = 1)
        {
            /** Constructs a Color[,] 'image' matrix: Gaussian centred at centre of image. */
            /** ideally size is an odd number */

            Color setClr = defaultImageClr;
            if (centreClr.HasValue) setClr = centreClr.Value;

            double centX = (size - 1) / 2f;
            double centY = (size - 1) / 2f;
            double maxDist = Helper.GetDistance(centX, centY);

            Color[,] byteMap = new Color[size, size];

            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var dist = Helper.GetDistance(x, y, centX, centY);
                    var relDist = dist / maxDist;
                    var setVal = gaussianVal(relDist, width: 0.707) * setClr.A * mulA; //*setClr.R
                    if (setVal < 0) setVal = 0;
                    if (setVal > 255) setVal = 255;
                    byteMap[x, y] = Color.FromArgb(Convert.ToByte(setVal), setClr.R, 0, 0);//Color.FromArgb(255, Convert.ToByte(setVal), 0, 0);
                }
            return byteMap;
        }
        static Color[,] matrixImage(int width, int height, cRandomHelper randomHelper)
        {
            /** Constructs a Color[,] 'image' matrix: random terrain.
             * Merges gradually higher detail images together. Means each pixel is more similar to it's local pixels.*/
            Color[,] map = matrixPlain(width, height, Color.FromArgb(Convert.ToByte(255), Convert.ToByte(0), Convert.ToByte(0), Convert.ToByte(0)));

            double[,] kernel;


            int curW = 2;
            int curH = 2;
            while (curW <= width && curH <= height)
            {
                int range = (int)(60f / (float)(1 + (0.035 * curW)));

                Color[,] mask1 = matrixNoise(curW, curH, randomHelper, range); //15

                //Smooth

                mask1 = scaleImage(mask1, width, height); //Scale mask to img size

                int setKernelSize = (int)Math.Round(width / (2d * curW));
                if (setKernelSize > 1)
                {
                    kernel = kernelPlain(setKernelSize, 1f / (setKernelSize * setKernelSize));
                    mask1 = conv2DMask(mask1, kernel);
                }

                addMask(map, mask1);

                curW *= 2;
                curH *= 2;
            }

            return map;
        }
        static Color[,] matrixCircle(int size, Color? circleClr = null, Color? notCircleClr = null)
        {
            /** Constructs a Color[,] 'image' matrix: Circle plain colour. */
            Color setCircleVal = defaultImageClr;
            if (circleClr.HasValue) setCircleVal = circleClr.Value;

            Color setNotCircleVal = defaultImageClr;
            if (notCircleClr.HasValue) setNotCircleVal = notCircleClr.Value;

            Color[,] byteMap = new Color[size, size];

            var c = size / 2f;

            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var dist = Helper.GetDistance(x, y, c, c);
                    if (dist < c) byteMap[x, y] = Color.FromArgb(setCircleVal.A, setCircleVal.R, setCircleVal.G, setCircleVal.B);
                    else byteMap[x, y] = Color.FromArgb(setNotCircleVal.A, setNotCircleVal.R, setNotCircleVal.G, setNotCircleVal.B);
                }

            return byteMap;
        }
        static Color[,] matrixCopy(Color[,] copyMap)
        {
            /** Constructs a Color[,] 'image' matrix: Copies input image */
            var width = copyMap.GetUpperBound(0) + 1;
            var height = copyMap.GetUpperBound(1) + 1;
            Color[,] byteMap = new Color[width, height];

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) byteMap[x, y] = Color.FromArgb(copyMap[x, y].A, copyMap[x, y].R, copyMap[x, y].G, copyMap[x, y].B);

            return byteMap;
        }
        static Color[,] matrixCopy(int width, int height, Color[,] copyMap)
        {
            /** Constructs a Color[,] 'image' matrix: Copies input image w resizing */
            /// Creates image with new bounds. Makes a resized copy of input image
            /// Uses nearest neigbor for creating smaller images
            var imWidth = copyMap.GetUpperBound(0) + 1;
            var imHeight = copyMap.GetUpperBound(1) + 1;
            Color[,] byteMap = new Color[width, height];

            float xScale = (float)imWidth / (float)width;
            float yScale = (float)imHeight / (float)height;

            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    var refX = (int)((xScale * x) + (xScale / 2f));
                    var refY = (int)((yScale * y) + (yScale / 2f));

                    var mkCell = copyMap[refX, refY]; //copyMap[(int)Math.Round(((float)x / (float)width) * (imWidth - 1)), (int)Math.Round(((float)y / (float)height) * (imHeight - 1))];
                    byteMap[x, y] = Color.FromArgb(mkCell.A, mkCell.R, mkCell.G, mkCell.B);


                }

            return byteMap;
        }
        static Color[,] matrixFromKernel(float[,] copyMap, Color? clr = null)
        {
            /** Constructs a Color[,] 'image' matrix: Copies input kernel */
            /// Creates image with new bounds. Makes a resized copy of input image
            /// Uses nearest neigbor for creating smaller images
            var imWidth = copyMap.GetUpperBound(0) + 1;
            var imHeight = copyMap.GetUpperBound(1) + 1;
            Color[,] byteMap = new Color[imWidth, imHeight];

            Color setClr;
            if (clr.HasValue) setClr = clr.Value;
            else setClr = defaultImageClr;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var refX = (int)((x));
                    var refY = (int)((y));

                    var mkCell = copyMap[refX, refY]; //copyMap[(int)Math.Round(((float)x / (float)width) * (imWidth - 1)), (int)Math.Round(((float)y / (float)height) * (imHeight - 1))];
                    byteMap[x, y] = Color.FromArgb(Convert.ToByte((int)mkCell), setClr.R, setClr.G, setClr.B);


                }

            return byteMap;
        }
        static Color[,] matrixCropped(int width, int height, int leftX, int topY, Color[,] copyMap)
        {
            /** Constructs a Color[,] 'image' matrix: Creates cropped version of input image. */
            var copyWidth = copyMap.GetUpperBound(0) + 1;
            var copyHeight = copyMap.GetUpperBound(1) + 1;

            var endX = Math.Min(copyWidth, leftX + width);
            var endY = Math.Min(copyHeight, topY + height);

            var newWidth = Math.Min(width, endX - leftX);
            var newHeight = Math.Min(height, endY - topY);

            Color[,] byteMap = new Color[newWidth, newHeight];

            for (int y = topY; y < endY; y++) for (int x = leftX; x < endX; x++) byteMap[x - leftX, y - topY] = Color.FromArgb(copyMap[x, y].A, copyMap[x, y].R, copyMap[x, y].G, copyMap[x, y].B);

            return byteMap;
        }

        //Kernels
        /** Kernels are 2D arrays. Each 'pixel' holds a single fractional value. These can be used to apply effects to images. */
        public static double[,] kernelGaussian(int size, bool normalizeValues = true, double maxValue = 1)
        {
            /** ideally size is an odd number. Max value can be any real number. */

            double centX = (size - 1) / 2f;
            double centY = (size - 1) / 2f;
            double maxDist = Helper.GetDistance(centX, centY);

            double[,] kernel = new double[size, size];

            double totalVal = 0;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var dist = Helper.GetDistance(x, y, centX, centY);
                    var relDist = dist / maxDist;
                    var setVal = gaussianVal(dist, var: 0.3 * (size * 0.5));//gaussianVal(relDist, width: 0.707) * maxValue;
                    kernel[x, y] = setVal;
                    totalVal += setVal;
                }

            if (normalizeValues)
            {
                //Makes sure kernel total val = 1, so does not lighten or darken image
                double normalize = 1 / totalVal;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        kernel[x, y] = kernel[x, y] * normalize;
                    }
            }

            return kernel;
        }
        public static double[,] kernelPlain(int size, double setVal = 1)
        {
            /** ideally size is an odd number. Max value can be any real number. */

            double[,] kernel = new double[size, size];

            double totalVal = 0;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    kernel[x, y] = setVal;
                    totalVal += setVal;
                }

            return kernel;
        }

        public static double gaussianVal(double x, double var = 0.3, double width = 1)
        {
            /** Uses a gaussian function where:
             * - Centre is at x=0
             * - Max y value roughly = 1
             * 
             * When var = 0.3:
             * - Where x <= -1 or x >= 1, y roughly = 0
             * 
             * When var = 0.2:
             * - Where x = +-0.707, y roughly = 0
             * V useful when generating kernels, as 0.707 is the closest dist of an edge pixel.
             * 
             * Width is a better parameter for manipulating kernel size without changing the maxval.
             * width = 1: Default. Provided no other changed parameters, where x = +-1, y = 0
             * Width = 0.5: x = +-0.5, y roughly = 0
             * Width = 2: x = +-2, y roughly = 0 
             * 
             */
            double v = var; //0.75f *
            return (1 / (v * Math.Sqrt(2 * Math.PI))) * Math.Exp(-0.5 * Math.Pow(x / (v * width), 2));
        }


        //Heatmap Functions
        /** Misc */
        public static void reduceOpacity(ref Color[,] map, double opacityMul)
        {
            /** Reduces opacity of input image.
             * 
             * 1 = no change
             * 0.5 = opacity is halved
             * 1.5 = opacity increases by 1.5
             * 
             * Opacity will be capped at 255.
             * Decreasing opacity will reduce the resolution of opacity values (clipping effect).
             * 
             */

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            int max = 0;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    map[x, y].A = Convert.ToByte(Math.Min(map[x, y].A * opacityMul, 255));
                }
        }
        public static void paintMask(Color[,] map, Color[,] mask, double multiplier = 1, bool stretchFit = false, float maskLeftX = 0, float maskTopY = 0)
        {
            /**
             * Paints mask to image:
             * Paint 'replaces' existing pixel value. This effect is dependent on input opacity.
             * 
             * Stretches mask to fit image if set.
             * 
             * Mask is treated as such:
             * 
             * Values range from (0 -> 128 -> 255)
             * Applies value as relative to 128. Eg:
             * 128 -> No change
             * 127 -> -1
             * 129 -> +1
             * 
             */

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;

            double endX = Math.Min(maskLeftX + mkWidth, imWidth);
            double endY = Math.Min(maskTopY + mkHeight, imHeight);
            if (stretchFit) { endX = imHeight; endY = imHeight; }

            for (int y = (int)Math.Max(0, maskTopY); y < endY; y++) for (int x = (int)Math.Max(0, maskLeftX); x < endX; x++)
                {
                    var mkX = x - maskLeftX;
                    var mkY = y - maskTopY;

                    if (mkX < 0 || mkX >= mkWidth || mkY < 0 || mkY >= mkHeight) continue;

                    Color mkCell;
                    if (stretchFit) mkCell = mask[(int)Math.Round(((float)mkX / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)mkY / (float)imHeight) * (mkHeight - 1))];
                    else mkCell = mask[(int)mkX, (int)mkY];

                    if (mkCell.A > 1 && mkCell.A < 255) ;
                    if (mkCell.R != 255 && mkCell.R != 0) ;

                    var changeVal = ((float)mkCell.R - 128f) * multiplier * (mkCell.A / 255f); //didnt have * mkcell.A/255 bit
                    var resultVal = map[x, y].R + changeVal;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    paintOntoC1(ref map[x, y], mkCell);
                    var check = map[x, y];

                }
        }
        public static int maxR(Color[,] image)
        {
            /** Gets maximum r value in image */
            var imWidth = image.GetUpperBound(0) + 1;
            var imHeight = image.GetUpperBound(1) + 1;

            int max = 0;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    if (image[x, y].R > max) max = image[x, y].R;
                }
            return max;
        }

        /** Adds R values */
        public static void addMask(Color[,] map, Color[,] mask, double multiplier = 1, bool stretchFit = false, float maskLeftX = 0, float maskTopY = 0, int maskZeroValue = 128)
        {
            /**
             * Adds mask to image:
             * Adding increases the R value of target pixel, subject to input pixels R and A value.
             * 
             * Stretches mask to fit image.
             * 
             * Mask is treated as such:
             * 
             * Values range from (0 -> 128 -> 255)
             * Applies value as relative to 128. Eg:
             * 128 -> No change
             * 127 -> -1
             * 129 -> +1
             * 
             */

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;

            /** maskLeftX = Gap until mask starts. Moves mask right when value is positive (adds buffer on left of image). */

            double endX = Math.Min(maskLeftX + mkWidth, imWidth);
            double endY = Math.Min(maskTopY + mkHeight, imHeight);
            if (stretchFit) { endX = imHeight; endY = imHeight; }

            for (int y = (int)Math.Max(0, maskTopY); y < endY; y++) for (int x = (int)Math.Max(0, maskLeftX); x < endX; x++)
                {
                    var mkX = x - maskLeftX;
                    var mkY = y - maskTopY;

                    if (mkX < 0 || mkX >= mkWidth || mkY < 0 || mkY >= mkHeight) continue;

                    Color mkCell; //mkCell = mask[(int)Math.Round(((float)x / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)y / (float)imHeight) * (mkHeight - 1))];
                    if (stretchFit) mkCell = mask[(int)Math.Round(((float)mkX / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)mkY / (float)imHeight) * (mkHeight - 1))];
                    else mkCell = mask[(int)mkX, (int)mkY];

                    if (mkCell.R > 2) ;

                    var changeVal = ((float)mkCell.R - maskZeroValue) * multiplier * (mkCell.A / 255f); //didnt have * mkcell.A/255 bit
                    var resultVal = map[x, y].R + changeVal;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    map[x, y].R = Convert.ToByte(resultVal);
                }
        }
        public static void addMask(Color[,] map, double[,] mask, double multiplier = 1)
        {
            /**
             * Adds mask to image:
             * Adding increases the R value of target pixel, subject to input pixels R and A value.
             * 
             * Stretches mask to fit image.
             * 
             * Mask is treated as such:
             * 
             * Values range from (0 -> 128 -> 255)
             * Applies value as relative to 128. Eg:
             * 128 -> No change
             * 127 -> -1
             * 129 -> +1
             * 
             */

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {

                    var mkCell = mask[(int)Math.Round(((float)x / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)y / (float)imHeight) * (mkHeight - 1))];
                    var changeVal = ((float)mkCell - 128f) * multiplier;
                    var resultVal = map[x, y].R + changeVal;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    map[x, y].R = Convert.ToByte(resultVal);

                }
        }

        /** Merges R values */
        public static void mergeBlendMask(Color[,] map, Color[,] mask, double imageProp = 0.5, bool stretchFit = false, float maskLeftX = 0, float maskTopY = 0)
        {
            /**
             * Blends mask to image:
             * Averages map pixel and mask pixel by imageProp.
             * 
             * Stretches mask to fit image. 
             * 
             * Averages values between image and mask. 
             * ImageProp defines the extent to which image is drawn to mask being drawn:
             * 0 => No change to image
             * 0.25 => 75% image, 25% mask
             * 0.5 => Proper average; 50% image, 50% mask
             * 0.25 => 25% image, 75% mask
             * 1 => Returns mask stretched to bounds of image
             * 
             */
            if (imageProp < 0) imageProp = 0; //Cant be more than 100% image
            if (imageProp > 1) imageProp = 1; //Cant be more than 100% mask  
            double maskProp = 1f - imageProp;

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var mkX = x - maskLeftX;
                    var mkY = y - maskTopY;

                    if (mkX < 0 || mkX >= mkWidth || mkY < 0 || mkY >= mkHeight) continue;

                    Color mkCell; //mkCell = mask[(int)Math.Round(((float)x / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)y / (float)imHeight) * (mkHeight - 1))];
                    if (stretchFit) mkCell = mask[(int)Math.Round(((float)mkX / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)mkY / (float)imHeight) * (mkHeight - 1))];
                    else mkCell = mask[(int)mkX, (int)mkY];

                    var resultVal = ((map[x, y].R * maskProp) + (mkCell.R * imageProp)) / 2f;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    map[x, y].R = Convert.ToByte(resultVal);
                }
        }
        public static void mergeBlendMask(Color[,] map, double[,] mask, double imageProp = 0.5)
        {
            /**
             * Blends mask to image:
             * Averages map pixel and mask pixel by imageProp.
             * 
             * Stretches mask to fit image. 
             * 
             * Averages values between image and mask. 
             * ImageProp defines the extent to which image is drawn to mask being drawn:
             * 0 => No change to image
             * 0.25 => 75% image, 25% mask
             * 0.5 => Proper average; 50% image, 50% mask
             * 0.25 => 25% image, 75% mask
             * 1 => Returns mask stretched to bounds of image
             * 
             */
            if (imageProp < 0) imageProp = 0; //Cant be more than 100% image
            if (imageProp > 1) imageProp = 1; //Cant be more than 100% mask  
            double maskProp = 1f - imageProp;

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var mkCell = mask[(int)Math.Round(((float)x / (float)imWidth) * (mkWidth - 1)), (int)Math.Round(((float)y / (float)imHeight) * (mkHeight - 1))];
                    var resultVal = ((map[x, y].R * maskProp) + (mkCell * imageProp)) / 2f;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    map[x, y].R = Convert.ToByte(resultVal);
                }
        }

        /** Convolves map R values using mask */
        public static Color[,] conv2DMask(Color[,] map, Color[,] mask, double multiplier = 1)
        {
            /**
             * Convolves mask to image:
             * 
             * Convolving is a common operation when manipulating images. A good example is blurring an image using a gaussian kernel.
             * Convolving is where the values of each pixel in an image are re-calculated. 
             * Their new values are calculated by summing the values of pixels around them, as defined by the 'mask' kernel.
             * Explanation with examples: https://setosa.io/ev/image-kernels/             * 
             * 
             */


            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            Color[,] outputImage = new Color[imWidth, imHeight];

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {

                    //Calculate convolved value
                    var convStartx = x - mkWidth / 2;
                    var convStarty = y - mkHeight / 2;

                    var convEndx = x + mkWidth / 2;
                    var convEndy = y + mkHeight / 2;

                    double curVal = 0;

                    double missedPixels = 0;

                    int maskx = 0;
                    int masky = 0;
                    for (int cy = convStarty; cy < convEndy; cy++)
                    {
                        maskx = 0;
                        for (int cx = convStartx; cx < convEndx; cx++)
                        {
                            if (cy > 0 && cy < imHeight && cx > 0 && cx < imWidth) curVal += map[cx, cy].R * mask[maskx, masky].R;
                            else missedPixels += 1;
                            maskx += 1;
                        }
                        masky += 1;
                    }

                    //Stop edges from darkening
                    curVal *= (mkWidth * mkHeight) / ((mkWidth * mkHeight) - missedPixels);

                    //Sanitizer and set convolved value
                    var resultVal = curVal;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    map[x, y].R = Convert.ToByte(resultVal);

                }

            return outputImage;
        }
        public static Color[,] conv2DMask(Color[,] map, double[,] mask, double multiplier = 1)
        {
            /**
             * Convolves mask to image:
             * 
             * Convolving is a common operation when manipulating images. A good example is blurring an image using a gaussian kernel.
             * Convolving is where the values of each pixel in an image are re-calculated. 
             * Their new values are calculated by summing the values of pixels around them, as defined by the 'mask' kernel.
             * Explanation with examples: https://setosa.io/ev/image-kernels/             
             * 
             */


            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            Color[,] outputImage = new Color[imWidth, imHeight];

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    if (map[x, y].R > 170) ;

                    //Calculate convolved value
                    var convStartx = x - mkWidth / 2;
                    var convStarty = y - mkHeight / 2;

                    var convEndx = x + mkWidth / 2;
                    var convEndy = y + mkHeight / 2;

                    double curRVal = 0;

                    double missedPixels = 0;

                    int maskx = 0;
                    int masky = 0;
                    for (int cy = convStarty; cy <= convEndy; cy++)
                    {
                        maskx = 0;
                        for (int cx = convStartx; cx <= convEndx; cx++)
                        {
                            if (cy >= 0 && cy < imHeight && cx >= 0 && cx < imWidth) { curRVal += map[cx, cy].R * mask[maskx, masky]; }
                            else missedPixels += 1;
                            maskx += 1;
                        }
                        masky += 1;
                    }
                    //Stop edges from darkening
                    curRVal *= (mkWidth * mkHeight) / ((mkWidth * mkHeight) - missedPixels);

                    //Sanitizer and set convolved value
                    var resultVal = curRVal;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;

                    outputImage[x, y] = Color.FromArgb(255, Convert.ToByte(resultVal), 0, 0);

                }

            return outputImage;
        }
        /** Image function */
        public static Color[,] conv2DMaskColorOpacity(Color[,] image, double[,] mask, double multiplier = 1)
        {
            /**
             * Convolves mask to image:
             * 
             * Convolving is a common operation when manipulating images. A good example is blurring an image using a gaussian kernel.
             * Convolving is where the values of each pixel in an image are re-calculated. 
             * Their new values are calculated by summing the values of pixels around them, as defined by the 'mask' kernel.
             * Explanation with examples: https://setosa.io/ev/image-kernels/             
             * 
             * Other method only affects R value. This one convolves A value, and merges colours based on that.
             * 
             */


            var imWidth = image.GetUpperBound(0) + 1;
            var imHeight = image.GetUpperBound(1) + 1;

            var mkWidth = mask.GetUpperBound(0) + 1;
            var mkHeight = mask.GetUpperBound(1) + 1;


            Color[,] outputImage = new Color[imWidth, imHeight];

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    if (image[x, y].R > 170) ;

                    //Calculate convolved value
                    var convStartx = x - mkWidth / 2;
                    var convStarty = y - mkHeight / 2;

                    var convEndx = x + mkWidth / 2;
                    var convEndy = y + mkHeight / 2;

                    double curAVal = 0;

                    double missedPixels = 0;

                    Color outputClr = Color.FromArgb(0, 0, 0, 0);

                    int maskx = 0;
                    int masky = 0;
                    for (int cy = convStarty; cy <= convEndy; cy++)
                    {
                        maskx = 0;
                        for (int cx = convStartx; cx <= convEndx; cx++)
                        {
                            if (cy >= 0 && cy < imHeight && cx >= 0 && cx < imWidth)
                            {
                                //Convolve A value
                                var aEffect = image[cx, cy].A * mask[maskx, masky];
                                curAVal += aEffect;

                                paintOntoC1(ref outputClr, Color.FromArgb(Convert.ToByte(aEffect), image[cx, cy].R, image[cx, cy].G, image[cx, cy].B));

                            }
                            else missedPixels += 1;
                            maskx += 1;
                        }
                        masky += 1;
                    }
                    //Stop edges from darkening
                    curAVal *= (mkWidth * mkHeight) / ((mkWidth * mkHeight) - missedPixels);

                    //Sanitizer and set convolved value

                    curAVal = Math.Min(255, Math.Max(0, curAVal));

                    outputClr.A = Convert.ToByte(curAVal);
                    outputImage[x, y] = outputClr;

                }

            return outputImage;
        }

        /** Rain erosion simulation on R values */
        public static void rainErode(Color[,] image, int iterations, cRandomHelper randomHelper, out Color[,] outRainImage, float[,] inRainMap = null, int inCurrentRaindrops = 0)
        {
            /**
             * This works with a matrix of floats so it can use fractional values.
             * 
             * Simulates water droplets eroding the image.
             * 
             * R values are used as height value of image 'landscape'.
             
             */


            var imWidth = image.GetUpperBound(0) + 1;
            var imHeight = image.GetUpperBound(1) + 1;

            float[,] map = new float[imWidth, imHeight];

            float maxRainVal = -1;
            float[,] rainMap = inRainMap;
            if (rainMap == null) rainMap = new float[imWidth, imHeight];
            int currentRainDrops = inCurrentRaindrops;

            //Populate map
            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    map[x, y] = image[x, y].R;
                    if (inRainMap != null)
                    {
                        //rainMap[x, y] = inRainMap[x, y];
                        if (inRainMap[x, y] > maxRainVal) maxRainVal = inRainMap[x, y];
                        //currentRainDrops += (int)inRainMap[x, y]; //Not how it works
                    }
                }

            var test = true;

            #region run rain erosion 

            for (int i = 0; i < iterations; i++)
            {
                currentRainDrops += 1;
                //Greater the value, the stronger fx of streams displacing water
                float waterFxMul = 30f / (1000 + currentRainDrops); // 33000f / (1000 + currentRainDrops);

                float dSize = 1; //Size will stop droplet sim if doesn't make progress
                float dSediment = 0;

                float dx = randomHelper.RandomNumber(0, imWidth - 1);
                float dy = randomHelper.RandomNumber(0, imWidth - 1);


                //Stops a bug where droplet is trapped
                int oldx = -1;
                int oldy = -1;

                bool complete = false;

                while (!complete)
                {
                    //Run droplet until reaches minimum it cannot escape OR droplet shrinks into nothingness
                    int dix = (int)Math.Round(dx);
                    int diy = (int)Math.Round(dy);

                    var curCell = map[dix, diy];

                    // - Travel

                    //Move these out of while loop to have momentum
                    float dxv = 0;
                    float dyv = 0;
                    bool someSlope = false; //Fixes cases where equal slope in all directions

                    /** Slopes can only pull in their direction. They cannot push in the other. */
                    //Left
                    if (dix > 0)
                    {
                        var cellH = map[dix - 1, diy] + (rainMap[dix - 1, diy] * waterFxMul);
                        dxv += Math.Min(0, (cellH - curCell));
                    }
                    if (dxv != 0) someSlope = true;
                    //Right
                    if (dix < (imWidth - 1))
                    {
                        var cellH = map[dix + 1, diy] + (rainMap[dix + 1, diy] * waterFxMul);
                        dxv += Math.Max(0, (curCell - cellH));
                    }
                    if (dxv != 0) someSlope = true;
                    //Bottom
                    if (diy > 0)
                    {
                        var cellH = map[dix, diy - 1] + (rainMap[dix, diy - 1] * waterFxMul);
                        dyv += Math.Min(0, (cellH - curCell));
                    }
                    if (dyv != 0) someSlope = true;
                    //Top
                    if (diy < (imWidth - 1))
                    {
                        var cellH = map[dix, diy + 1] + (rainMap[dix, diy + 1] * waterFxMul);
                        dyv += Math.Max(0, (curCell - cellH));
                    }
                    if (dyv != 0) someSlope = true;

                    if (someSlope && dxv == 0 && dyv == 0) dxv = 1;

                    //Make sure we only travel 1 pixel
                    if (dxv != 0 || dyv != 0)
                    {
                        float denom = (Math.Abs(dxv) + Math.Abs(dyv));
                        dxv = dxv / denom;
                        dyv = dyv / denom;
                    }
                    else
                    {
                        dxv = 0;
                        dyv = 0;
                    }

                    int dNewix = (int)Math.Round(dx + dxv);
                    int dNewiy = (int)Math.Round(dy + dyv);

                    if (dNewix > 0 && dNewix < imWidth && dNewiy > 0 && dNewiy < imHeight)
                    {

                        var nextCell = map[dNewix, dNewiy];

                        // - Sediment change

                        //Positive means next cell higher, negative means next cell lower
                        float heightDiff = nextCell - curCell;

                        float sedimentDropConst = -0.1f;  //Makes sure drops some sediment when on even surface.
                        float sedimentSlopeChange = -heightDiff;

                        float erosionRate = 0.3f;
                        float dropRate = 0.2f; //1f //1 -> drops all sediment required to climb gap

                        //Positive sedimentChange means droplet picks up sediment. Negative means it drops sediment.
                        float sedimentChange;

                        //Going up, going to drop sediment
                        if (heightDiff > 0) sedimentChange = sedimentDropConst + (sedimentSlopeChange * dropRate);
                        //Going down, going to gain sediment
                        else sedimentChange = sedimentDropConst + (erosionRate * sedimentSlopeChange);

                        if (-sedimentChange > dSediment) sedimentChange = -dSediment;

                        map[dix, diy] += -sedimentChange;
                        dSediment += sedimentChange;

                        // Runs out of sediment and is in a minimum
                        if (dSediment == 0 && dix == dNewix && diy == dNewiy) complete = true;
                        if (dSediment == 0 && oldx == dNewix && oldy == dNewiy) complete = true;

                        if (dSediment == 0) dSize -= 0.01f;
                        if (dSize <= 0) complete = true;

                        dx += dxv;
                        dy += dyv;

                        oldx = dix;
                        oldy = diy;

                        //rainvals
                        rainMap[dNewix, dNewiy] += 1;
                        if (rainMap[dNewix, dNewiy] > maxRainVal) maxRainVal = rainMap[dNewix, dNewiy];

                    }
                    else
                    {
                        //Travelled out of the map
                        //TODO drop some sediment?
                        complete = true;
                    }
                }

            }


            #endregion

            //Populate image
            var maxRatio = maxRainVal / currentRainDrops;
            outRainImage = new Color[imWidth, imHeight];

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var d = image[x, y].R;
                    var r = Math.Round(map[x, y]);
                    //if (Math.Abs(d - r) > 3) ;
                    image[x, y].R = Convert.ToByte(Math.Round(map[x, y]));
                    //image[x, y].G = Convert.ToByte(Math.Max(Math.Min(255, 2 * (d - r)), 0));
                    //image[x, y].B = Convert.ToByte(Math.Max(Math.Min(255, 2 * (r - d)), 0));

                    outRainImage[x, y] = Color.FromArgb(Convert.ToByte((rainMap[x, y] * 255 / (float)maxRainVal) * (1000f / currentRainDrops)), 20, 100, 255);

                }

            bool done = true;
        }
       
        /** Transforms image to specified boundaries */
        public static Color[,] scaleImage(Color[,] image, int width, int height)
        {
            /**
             * Blends mask to image:
             * 
             * Stretches mask to fit image. 
             * 
             * Averages values between image and mask. 
             * ImageProp defines the extent to which image is drawn to mask being drawn:
             * 0 => No change to image
             * 0.25 => 75% image, 25% mask
             * 0.5 => Proper average; 50% image, 50% mask
             * 0.25 => 25% image, 75% mask
             * 1 => Returns mask stretched to bounds of image
             * 
             */
            if (width < 0) return null; //Cant be less than 0 wide
            if (height < 0) return null; //Cant be less than 0 wide


            var imWidth = image.GetUpperBound(0) + 1;
            var imHeight = image.GetUpperBound(1) + 1;

            var toWidth = width;
            var toHeight = height;

            Color[,] outImg = new Color[width, height];

            for (int y = 0; y < toHeight; y++) for (int x = 0; x < toWidth; x++)
                {
                    var mkCell = image[(int)Math.Round(((float)x / (float)toWidth) * (imWidth - 1)), (int)Math.Round(((float)y / (float)toWidth) * (imHeight - 1))];

                    outImg[x, y] = Color.FromArgb(mkCell.A, mkCell.R, mkCell.G, mkCell.B);
                }

            return outImg;
        }

        /** Creates full color image using map and color lookup */
        static void ApplyColorLookup(Color[,] outImg, Color[,] lookupImg, int lookupRValue, LookupFunction lf, Color setClr)
        {
            /** Creates full color image using R value lookup. */

            var imWidth = lookupImg.GetUpperBound(0) + 1;
            var imHeight = lookupImg.GetUpperBound(1) + 1;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var cell = lookupImg[x, y];
                    if (Math.Abs(cell.R - lookupRValue) < 20) ;
                    var resultVal = lf.lookup(cell.R, lookupRValue);
                    resultVal *= 255f;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    resultVal *= (setClr.A / 255f);

                    var paint = Color.FromArgb(Convert.ToByte(resultVal), setClr.R, setClr.G, setClr.B);

                    outImg[x, y] = aiMapGen.getPaintedClr(outImg[x, y], paint);
                }
        }
        static void ApplyColorLookupGreedy(Color[,] outImg, Color[,] lookupImg, int lookupRValue, LookupFunction lf, Color setClr)
        {
            /** /** Creates full color image using R value lookup. 
            *Use this method if outImg is a different size to lookup Image. Is greedy (if any pixels are true that map to a single pixel in output image, output pixel is true. */

            var imWidth = lookupImg.GetUpperBound(0) + 1;
            var imHeight = lookupImg.GetUpperBound(1) + 1;

            var outWidth = outImg.GetUpperBound(0) + 1;
            var outHeight = outImg.GetUpperBound(1) + 1;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var cell = lookupImg[x, y];
                    if (Math.Abs(cell.R - lookupRValue) < 20) ;
                    var resultVal = lf.lookup(cell.R, lookupRValue);
                    resultVal *= 255f;
                    if (resultVal < 0) resultVal = 0;
                    if (resultVal > 255) resultVal = 255;
                    resultVal *= (setClr.A / 255f);

                    var paint = Color.FromArgb(Convert.ToByte(resultVal), setClr.R, setClr.G, setClr.B);

                    var outX = (int)(((float)x / (float)imWidth) * outWidth);
                    var outY = (int)(((float)y / (float)imHeight) * outHeight);
                    var set = aiMapGen.getPaintedClr(outImg[outX, outY], paint);

                    if (outImg[outX, outY].A < set.A) outImg[outX, outY] = set;
                }
        }

        /** Painting functions */
        public static Color getPaintedClr(Color c1, Color c2)
        {
            /** NOT the same as a merge. This is as if c2 is painted ontop of c1. */
            if (c1 != null && (c1.A != 0 || c2.A != 0))
            {

                var setA = c1.A + ((255 - c1.A) * (c2.A / 255f));

                //If c2 is opaque, then there will be no color from c1 left
                var c1Part = c1.A * (1 - (c2.A / 255f));
                var c2Part = c2.A * (c2.A / 255f);
                var c1Prop = c1Part / (c1Part + c2Part); //1 - (c2.A / 255f)
                var c2Prop = c2Part / (c1Part + c2Part); //(c2.A / 255f);

                var setR = (c1Prop * c1.R) + (c2Prop * c2.R);
                var setG = (c1Prop * c1.G) + (c2Prop * c2.G);
                var setB = (c1Prop * c1.B) + (c2Prop * c2.B);

                if (c2.A != 255 && c2.A != 0) ;
                if (setR != 184 || setG != 134 || setB != 11) ;

                return Color.FromArgb(Convert.ToByte(setA), Convert.ToByte(setR), Convert.ToByte(setG), Convert.ToByte(setB));
            }
            else return Color.FromArgb(c2.A, c2.R, c2.G, c2.B);
        }
        public static void paintOntoC1(ref Color c1, Color c2)
        {
            /** NOT the same as a merge. This is as if c2 is painted ontop of c1. */
            if (c1 != null && (c1.A != 0 || c2.A != 0))
            {

                var setA = c1.A + ((255 - c1.A) * (c2.A / 255f));

                //If c2 is opaque, then there will be no color from c1 left
                var c1Part = c1.A * (1 - (c2.A / 255f)); //c1.A
                var c2Part = c2.A * (c2.A / 255f); //c2.A
                var c1Prop = c1Part / (c1Part + c2Part); //1 - (c2.A / 255f)
                var c2Prop = c2Part / (c1Part + c2Part); //(c2.A / 255f);

                var setR = (c1Prop * c1.R) + (c2Prop * c2.R);
                var setG = (c1Prop * c1.G) + (c2Prop * c2.G);
                var setB = (c1Prop * c1.B) + (c2Prop * c2.B);

                if (c2.A != 255 && c2.A != 0) ;
                if (setR != 184 || setG != 134 || setB != 11) ;
                if (setR < c2.R && setR < c1.R) ;

                c1.A = Convert.ToByte(setA);
                c1.R = Convert.ToByte(setR);
                c1.G = Convert.ToByte(setG);
                c1.B = Convert.ToByte(setB);
            }
            else
            {
                c1.A = c2.A;
                c1.R = c2.R;
                c1.G = c2.G;
                c1.B = c2.B;
            }
        }
        public static void aThreshold(Color[,] outImg, Color[,] lookupImg, int thresholdAValue, Color setClr, Color falseClr, bool paintIfHigher = true)
        {
            /** Value must be equal or higher. If true, set pixel values to setClr. */

            var imWidth = lookupImg.GetUpperBound(0) + 1;
            var imHeight = lookupImg.GetUpperBound(1) + 1;

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var cell = lookupImg[x, y];
                    bool meetsThreshold = (paintIfHigher && cell.A >= thresholdAValue) || (!paintIfHigher && cell.A <= thresholdAValue);

                    Color paint;
                    if (meetsThreshold) paint = Color.FromArgb(setClr.A, setClr.R, setClr.G, setClr.B);
                    else paint = Color.FromArgb(falseClr.A, falseClr.R, falseClr.G, falseClr.B);

                    outImg[x, y] = aiMapGen.getPaintedClr(outImg[x, y], paint);
                }
        }

      
        //Conversions
        public static Color[,] FloatMapToImage(float[,] map)
        {
            /** Turns input float[,] kernel into a Color[,] image. */

            var imWidth = map.GetUpperBound(0) + 1;
            var imHeight = map.GetUpperBound(1) + 1;

            var outRainImage = new Color[imWidth, imHeight];

            float maxRainVal = 0;
            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    if (map[x, y] > maxRainVal) maxRainVal = map[x, y];
                }

            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    outRainImage[x, y] = Color.FromArgb(Convert.ToByte(map[x, y] * 255 / (float)maxRainVal), 20, 100, 255);
                }
            return outRainImage;
        }
        //Conversion specific for water erosion simulation
        public static Color[,] MapWithWaterEffect(Color[,] img, float[,] rainmap, int inCurrentRaindrops)
        {
            /** Draws water flow onto image. */
            var imWidth = img.GetUpperBound(0) + 1;
            var imHeight = img.GetUpperBound(1) + 1;

            var outCombinedImg = new Color[imWidth, imHeight];

            float maxRainVal = 0;
            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    if (rainmap[x, y] > maxRainVal) maxRainVal = rainmap[x, y];
                }


            //Get rain metrics
            int currentRainDrops = inCurrentRaindrops;
            float waterFxMul = 3000f / (1000 + currentRainDrops);

            //Generate combined image
            for (int y = 0; y < imHeight; y++) for (int x = 0; x < imWidth; x++)
                {
                    var imgCell = img[x, y];

                    var rainVal = rainmap[x, y] * waterFxMul;

                    var setR = Math.Min(255, Math.Max(0, imgCell.R + rainVal));

                    outCombinedImg[x, y] = Color.FromArgb(imgCell.A, Convert.ToByte(setR), imgCell.G, imgCell.B);
                }
            return outCombinedImg;
        }


        //Color Lookup Combinations 
        public static Color[,] blueSeaColorLookupCombo(Color[,] map)
        {
            var width = map.GetUpperBound(0) + 1;
            var height = map.GetUpperBound(1) + 1;

            var clrMap = matrixPlain(width, height, Color.FromArgb(0, 0, 0, 0));

            StressLF slf = new StressLF();
            GaussianLF glf = new GaussianLF();
            QuadraticLF qlf = new QuadraticLF();
            MaxQuadraticLF mqlf = new MaxQuadraticLF();
            BooleanLF blf = new BooleanLF();

            //Bottom morphing colours
            glf.range = 15;
            glf.maxVal = 0.4;
            ApplyColorLookup(clrMap, map, 26, glf, Colors.MediumTurquoise);
            glf.range = 16;
            glf.maxVal = 0.5;
            ApplyColorLookup(clrMap, map, 36, glf, Colors.CornflowerBlue);
            glf.range = 16;
            glf.maxVal = 0.4;
            ApplyColorLookup(clrMap, map, 58, glf, Colors.MediumTurquoise);
            qlf.range = 8;
            qlf.max = 0.6;
            ApplyColorLookup(clrMap, map, 63, qlf, Colors.CornflowerBlue);

            qlf.range = 2;
            qlf.max = 0.6;
            ApplyColorLookup(clrMap, map, 73, qlf, Colors.MediumSpringGreen);

            glf.range = 8;
            glf.maxVal = 0.3;
            ApplyColorLookup(clrMap, map, 74, glf, Colors.MediumTurquoise);
            qlf.range = 10;
            qlf.max = 0.6;
            ApplyColorLookup(clrMap, map, 82, qlf, Colors.CornflowerBlue);

            //Top colors
            glf.range = 20;
            glf.maxVal = 0.8;
            ApplyColorLookup(clrMap, map, 160, glf, Colors.Tan);
            qlf.range = 40;
            qlf.max = 2.5;
            ApplyColorLookup(clrMap, map, 170, qlf, Colors.RoyalBlue);
            qlf.range = 20;
            qlf.max = 1;
            ApplyColorLookup(clrMap, map, 180, qlf, Color.FromArgb(255, 50, 60, 230));
            mqlf.range = 9;
            ApplyColorLookup(clrMap, map, 200, mqlf, Color.FromArgb(255, 20, 20, 20));

            return clrMap;
        }
               
        //Output
        public static Color[] CreateBitmapBytes(Color[,] byteMap, int width, int height)
        {
            ///Code copied from saveAsCanvasBitmap

            //Premultiply color values (Images typically scale RGB values by opacity (A/255)
            //This can be done by changing CanvasBitmap to use CanvasAlphaMode.Straight, but this doesnt seem to work :/
            Color[] outpByteMap = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    if (byteMap[x, y].A == 255) ;
                    var scale = byteMap[x, y].A / 255f;
                    byteMap[x, y].R = Convert.ToByte(byteMap[x, y].R * scale);
                    byteMap[x, y].G = Convert.ToByte(byteMap[x, y].G * scale);
                    byteMap[x, y].B = Convert.ToByte(byteMap[x, y].B * scale);
                }

            //Convert to 1d array
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) outpByteMap[(y * height) + x] = byteMap[x, y];

            for (int i = 0; i < outpByteMap.Length; i++) if (outpByteMap[i].A != 0 && (outpByteMap[i].R != 184 || outpByteMap[i].G != 134 || outpByteMap[i].B != 11)) ;

            //Create bitmap and save as file   
            return outpByteMap;
        }
        public static CanvasBitmap CreateCanvasBitmap(ICanvasResourceCreator draw, Color[,] byteMap, int width, int height)
        {
            ///Code copied from saveAsCanvasBitmap

            //Premultiply color values (Images typically scale RGB values by opacity (A/255)
            //This can be done by changing CanvasBitmap to use CanvasAlphaMode.Straight, but this doesnt seem to work :/
            Color[] outpByteMap = CreateBitmapBytes(byteMap, width, height);
            //Create bitmap and save as file         

            CanvasBitmap bmp = CanvasBitmap.CreateFromColors(draw, outpByteMap, width, height);

            return bmp;
        }
        public static CanvasBitmap CreateCanvasBitmap(ICanvasResourceCreator draw, Color[] imageBytes, int width, int height)
        {
            ///Code copied from saveAsCanvasBitmap

            //Create bitmap and save as file         

            CanvasBitmap bmp = CanvasBitmap.CreateFromColors(draw, imageBytes, width, height);

            return bmp;
        }
        public static void saveAsCanvasBitmap(ICanvasResourceCreator draw, Color[,] byteMap, int width, int height, string filename)
        {

            //Premultiply color values (Images typically scale RGB values by opacity (A/255)
            //This can be done by changing CanvasBitmap to use CanvasAlphaMode.Straight, but this doesnt seem to work :/
            Color[] outpByteMap = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    if (byteMap[x, y].A == 255) ;
                    var scale = byteMap[x, y].A / 255f;
                    byteMap[x, y].R = Convert.ToByte(byteMap[x, y].R * scale);
                    byteMap[x, y].G = Convert.ToByte(byteMap[x, y].G * scale);
                    byteMap[x, y].B = Convert.ToByte(byteMap[x, y].B * scale);
                }

            //Convert to 1d array
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) outpByteMap[(y * height) + x] = byteMap[x, y];

            for (int i = 0; i < outpByteMap.Length; i++) if (outpByteMap[i].A != 0 && (outpByteMap[i].R != 184 || outpByteMap[i].G != 134 || outpByteMap[i].B != 11)) ;

            //Create bitmap and save as file         

            CanvasBitmap bmp = CanvasBitmap.CreateFromColors(draw, outpByteMap, width, height);//, 96f, CanvasAlphaMode.Premultiplied);

            string setFilename = filename + ".png";
            StorageFolder pictureFolder = KnownFolders.SavedPictures;

            //Get file location
            var fileTask = pictureFolder.CreateFileAsync(setFilename, CreationCollisionOption.ReplaceExisting).AsTask();
            fileTask.Wait();
            var file = fileTask.Result;

            var fsTask = file.OpenAsync(FileAccessMode.ReadWrite).AsTask();
            fsTask.Wait();
            var fs = fsTask.Result;
            using (var fileStream = fs)
            {
                var saveTask = bmp.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f).AsTask();
                saveTask.Wait();
            }

        }
        public static async void saveAsCanvasBitmapAsync(CanvasDrawingSession draw, Color[,] byteMap, int width, int height, string filename)
        {
            /**
             fileName: just the file name, with no path or filetype handle. eg: "test"
             */


            //Convert to 1d array
            Color[] outpByteMap = new Color[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) outpByteMap[(y * height) + x] = byteMap[x, y];

            //Create bitmap and save as file
            CanvasBitmap bmp = CanvasBitmap.CreateFromColors(draw, outpByteMap, width, height);

            string setFilename = filename + ".png";
            StorageFolder pictureFolder = KnownFolders.SavedPictures;
            var file = await pictureFolder.CreateFileAsync(setFilename, CreationCollisionOption.ReplaceExisting);
            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await bmp.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }

        }
        public static async void saveCanvasBitmapAsync(CanvasBitmap bmp, int width, int height, string filename)
        {
            /**
             fileName: just the file name, with no path or filetype handle. eg: "test"
             */

            string setFilename = filename + ".png";
            StorageFolder pictureFolder = KnownFolders.SavedPictures;
            var file = await pictureFolder.CreateFileAsync(setFilename, CreationCollisionOption.ReplaceExisting);
            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await bmp.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
            }

        }
        
    }


    /// Lookup Functions

    class LookupFunction
    {
        public virtual double lookup(double inputVal, double targetVal)
        {
            return 0;
        }
    }

    class QuadraticLF : LookupFunction
    {
        public double max = 1;
        public double range = 1;

        public override double lookup(double inputVal, double targetVal)
        {
            double x = inputVal - targetVal;
            return max - Math.Pow((1f / range) * x, 2);
        }
    }

    class MaxQuadraticLF : LookupFunction
    {
        public double max = 1;
        public double range = 1;
        public bool maxIfAbove = true;

        public override double lookup(double inputVal, double targetVal)
        {
            double x = inputVal - targetVal;
            if (maxIfAbove && x > 0) return max;
            else if (!maxIfAbove && x < 0) return max;
            return max - Math.Pow((1f / range) * x, 2);
        }
    }

    class StressLF : LookupFunction
    {

        /**
         * StressLF has a shape of 0.25 for lower, 1 for higher.
            
             */

        public double mul = 1;
        public double takeaway = 0.1;
        public double range = 1;

        public override double lookup(double inputVal, double targetVal)
        {
            double x = inputVal - targetVal;
            double s = 4d / range;
            double t = takeaway;

            double val = ((3 * mul) * (((s * x) + 1) * Math.Exp(-((s * x) + 1)))) - t;
            return Math.Max(val, 0);
        }
    }

    class GaussianLF : LookupFunction
    {

        /**
         * StressLF has a shape of 0.25 for lower, 1 for higher.
            
             */

        public double maxVal = 1;
        public double range = 10;

        public override double lookup(double inputVal, double targetVal)
        {
            double x = inputVal - targetVal;

            double val = maxVal * Math.Exp(-Math.Pow(x * (2 / range), 2));
            return Math.Max(val, 0);
        }
    }

    class BooleanLF : LookupFunction
    {
        public double max = 1;
        public double range = 1;

        public override double lookup(double inputVal, double targetVal)
        {
            double x = inputVal - targetVal;
            if (x < -range || x > range) return 0;
            else return max;
        }
    }

    ///Magic Number Functionality

    class MesaSettings
    {
        //Global settings
        public int? maxMesas;

        //Mesa brush
        public Point? brushPt;
        public int? brushMesaRadius;
        public Point? brushVel;
        public int? brushSteps;
        public int? brushCurrStep;
        public double? brushRadiusChangeVel;
        public int? brushAvgMesaSize;

        //Mesa Centres
        public double? lowerBoundCentreSizeMul;
        public bool? firstCentreIsAvgSize;

        //MesaBackground
        public double? mesaAreaCoverOpacity;
        public float? gaussMul = 2;
        public float? gaussAdd = 20;

        //Filling out mesas
        public int? totalBlobsToAdd;
        public int? totalBlobsToAddMul; //Will be overridden by totalBlobsToAdd
        public int? blurKernelSize;
        public double? lowerBoundFOMesaSize;
        public double? upperBoundFOMesaSize;
        public double? mesaBlobMul;
    }

}
