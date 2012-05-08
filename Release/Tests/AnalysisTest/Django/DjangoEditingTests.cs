﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using AnalysisTest.UI;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using TestUtilities;

namespace AnalysisTest.ProjectSystem {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class DjangoEditingTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Classification() {
            InsertionTest("Classification.html.djt", 8, 10, "",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 21, ">"),
                new Classifcation("HTML Tag Delimiter", 24, 26, "</"),
                new Classifcation("HTML Element Name", 26, 31, "title"),
                new Classifcation("HTML Tag Delimiter", 31, 34, "></"),
                new Classifcation("HTML Element Name", 34, 38, "head"),
                new Classifcation("HTML Tag Delimiter", 38, 39, ">"),
                new Classifcation("HTML Tag Delimiter", 41, 42, "<"),
                new Classifcation("HTML Element Name", 42, 46, "body"),
                new Classifcation("HTML Tag Delimiter", 46, 47, ">"),
                new Classifcation("Django template tag", 50, 52, "{%"),
                new Classifcation("keyword", 53, 63, "autoescape"),
                new Classifcation("Django template tag", 67, 69, "%}"),
                new Classifcation("Django template tag", 72, 74, "{%"),
                new Classifcation("keyword", 75, 85, "autoescape"),
                new Classifcation("Django template tag", 90, 92, "%}"),
                new Classifcation("Django template tag", 95, 97, "{%"),
                new Classifcation("keyword", 98, 108, "autoescape"),
                new Classifcation("Django template tag", 114, 116, "%}"),
                new Classifcation("Django template tag", 122, 124, "{%"),
                new Classifcation("keyword", 125, 132, "comment"),
                new Classifcation("Django template tag", 133, 135, "%}"),
                new Classifcation("Django template tag", 144, 146, "{%"),
                new Classifcation("keyword", 147, 157, "endcomment"),
                new Classifcation("Django template tag", 158, 160, "%}"),
                new Classifcation("Django template tag", 166, 168, "{%"),
                new Classifcation("keyword", 169, 173, "csrf"),
                new Classifcation("Django template tag", 174, 176, "%}"),
                new Classifcation("Django template tag", 181, 183, "{%"),
                new Classifcation("keyword", 184, 189, "cycle"),
                new Classifcation("excluded code", 189, 203, " 'row1' 'row2'"),
                new Classifcation("Django template tag", 204, 206, "%}"),
                new Classifcation("Django template tag", 209, 211, "{%"),
                new Classifcation("keyword", 212, 217, "cycle"),
                new Classifcation("excluded code", 217, 238, " 'row1' 'row2' as baz"),
                new Classifcation("Django template tag", 239, 241, "%}"),
                new Classifcation("Django template tag", 244, 246, "{%"),
                new Classifcation("keyword", 247, 252, "cycle"),
                new Classifcation("excluded code", 252, 256, " baz"),
                new Classifcation("Django template tag", 257, 259, "%}"),
                new Classifcation("Django template tag", 265, 267, "{%"),
                new Classifcation("keyword", 268, 273, "debug"),
                new Classifcation("Django template tag", 274, 276, "%}"),
                new Classifcation("Django template tag", 282, 284, "{%"),
                new Classifcation("keyword", 285, 291, "filter"),
                new Classifcation("identifier", 292, 304, "force_escape"),
                new Classifcation("identifier", 305, 310, "lower"),
                new Classifcation("Django template tag", 311, 313, "%}"),
                new Classifcation("Django template tag", 316, 318, "{%"),
                new Classifcation("keyword", 319, 328, "endfilter"),
                new Classifcation("Django template tag", 329, 331, "%}"),
                new Classifcation("Django template tag", 337, 339, "{%"),
                new Classifcation("keyword", 340, 347, "firstof"),
                new Classifcation("identifier", 348, 352, "var1"),
                new Classifcation("identifier", 353, 357, "var2"),
                new Classifcation("identifier", 358, 362, "var3"),
                new Classifcation("Django template tag", 363, 365, "%}"),
                new Classifcation("Django template tag", 370, 372, "{%"),
                new Classifcation("keyword", 373, 380, "ifequal"),
                new Classifcation("identifier", 381, 385, "user"),
                new Classifcation("Python dot", 385, 386, "."),
                new Classifcation("identifier", 386, 388, "id"),
                new Classifcation("Python dot", 388, 389, " "),
                new Classifcation("identifier", 389, 396, "comment"),
                new Classifcation("Python dot", 396, 397, "."),
                new Classifcation("identifier", 397, 404, "user_id"),
                new Classifcation("Python dot", 404, 405, " "),
                new Classifcation("Django template tag", 405, 407, "%}"),
                new Classifcation("Django template tag", 410, 412, "{%"),
                new Classifcation("keyword", 413, 423, "endifequal"),
                new Classifcation("Django template tag", 424, 426, "%}"),
                new Classifcation("Django template tag", 431, 433, "{%"),
                new Classifcation("keyword", 434, 441, "ifequal"),
                new Classifcation("identifier", 442, 446, "user"),
                new Classifcation("Python dot", 446, 447, "."),
                new Classifcation("identifier", 447, 449, "id"),
                new Classifcation("Python dot", 449, 450, " "),
                new Classifcation("identifier", 450, 457, "comment"),
                new Classifcation("Python dot", 457, 458, "."),
                new Classifcation("identifier", 458, 465, "user_id"),
                new Classifcation("Python dot", 465, 466, " "),
                new Classifcation("Django template tag", 466, 468, "%}"),
                new Classifcation("Django template tag", 471, 473, "{%"),
                new Classifcation("keyword", 474, 478, "else"),
                new Classifcation("Django template tag", 479, 481, "%}"),
                new Classifcation("Django template tag", 484, 486, "{%"),
                new Classifcation("keyword", 487, 497, "endifequal"),
                new Classifcation("Django template tag", 498, 500, "%}"),
                new Classifcation("Django template tag", 505, 507, "{%"),
                new Classifcation("keyword", 508, 518, "ifnotequal"),
                new Classifcation("identifier", 519, 523, "user"),
                new Classifcation("Python dot", 523, 524, "."),
                new Classifcation("identifier", 524, 526, "id"),
                new Classifcation("Python dot", 526, 527, " "),
                new Classifcation("identifier", 527, 534, "comment"),
                new Classifcation("Python dot", 534, 535, "."),
                new Classifcation("identifier", 535, 542, "user_id"),
                new Classifcation("Python dot", 542, 543, " "),
                new Classifcation("Django template tag", 543, 545, "%}"),
                new Classifcation("Django template tag", 548, 550, "{%"),
                new Classifcation("keyword", 551, 555, "else"),
                new Classifcation("Django template tag", 556, 558, "%}"),
                new Classifcation("Django template tag", 561, 563, "{%"),
                new Classifcation("keyword", 564, 577, "endifnotequal"),
                new Classifcation("Django template tag", 578, 580, "%}"),
                new Classifcation("Django template tag", 585, 587, "{%"),
                new Classifcation("keyword", 588, 598, "ifnotequal"),
                new Classifcation("identifier", 599, 603, "user"),
                new Classifcation("Python dot", 603, 604, "."),
                new Classifcation("identifier", 604, 606, "id"),
                new Classifcation("Python dot", 606, 607, " "),
                new Classifcation("identifier", 607, 614, "comment"),
                new Classifcation("Python dot", 614, 615, "."),
                new Classifcation("identifier", 615, 622, "user_id"),
                new Classifcation("Python dot", 622, 623, " "),
                new Classifcation("Django template tag", 623, 625, "%}"),
                new Classifcation("Django template tag", 628, 630, "{%"),
                new Classifcation("keyword", 631, 644, "endifnotequal"),
                new Classifcation("Django template tag", 645, 647, "%}"),
                new Classifcation("Django template tag", 652, 654, "{%"),
                new Classifcation("keyword", 655, 657, "if"),
                new Classifcation("identifier", 658, 661, "foo"),
                new Classifcation("Django template tag", 662, 664, "%}"),
                new Classifcation("Django template tag", 667, 669, "{%"),
                new Classifcation("keyword", 670, 675, "endif"),
                new Classifcation("Django template tag", 676, 678, "%}"),
                new Classifcation("Django template tag", 683, 685, "{%"),
                new Classifcation("keyword", 686, 688, "if"),
                new Classifcation("identifier", 689, 692, "foo"),
                new Classifcation("Django template tag", 693, 695, "%}"),
                new Classifcation("Django template tag", 698, 700, "{%"),
                new Classifcation("keyword", 701, 705, "else"),
                new Classifcation("Django template tag", 706, 708, "%}"),
                new Classifcation("Django template tag", 711, 713, "{%"),
                new Classifcation("keyword", 714, 719, "endif"),
                new Classifcation("Django template tag", 720, 722, "%}"),
                new Classifcation("Django template tag", 727, 729, "{%"),
                new Classifcation("keyword", 730, 733, "for"),
                new Classifcation("keyword", 736, 738, "in"),
                new Classifcation("Django template tag", 743, 745, "%}"),
                new Classifcation("Django template tag", 748, 750, "{%"),
                new Classifcation("keyword", 751, 757, "endfor"),
                new Classifcation("Django template tag", 758, 760, "%}"),
                new Classifcation("Django template tag", 765, 767, "{%"),
                new Classifcation("keyword", 768, 772, "load"),
                new Classifcation("Django template tag", 782, 784, "%}"),
                new Classifcation("Django template tag", 787, 789, "{%"),
                new Classifcation("keyword", 790, 794, "load"),
                new Classifcation("Django template tag", 808, 810, "%}"),
                new Classifcation("Django template tag", 815, 817, "{%"),
                new Classifcation("keyword", 818, 821, "now"),
                new Classifcation("excluded code", 821, 827, " 'Y H'"),
                new Classifcation("Django template tag", 828, 830, "%}"),
                new Classifcation("Django template tag", 835, 837, "{%"),
                new Classifcation("keyword", 838, 845, "regroup"),
                new Classifcation("excluded code", 845, 873, " people by gender as grouped"),
                new Classifcation("Django template tag", 874, 876, "%}"),
                new Classifcation("Django template tag", 881, 883, "{%"),
                new Classifcation("keyword", 884, 893, "spaceless"),
                new Classifcation("Django template tag", 894, 896, "%}"),
                new Classifcation("HTML Tag Delimiter", 900, 901, "<"),
                new Classifcation("HTML Element Name", 901, 902, "p"),
                new Classifcation("HTML Tag Delimiter", 902, 903, ">"),
                new Classifcation("HTML Tag Delimiter", 907, 909, "</"),
                new Classifcation("HTML Element Name", 909, 910, "p"),
                new Classifcation("HTML Tag Delimiter", 910, 911, ">"),
                new Classifcation("Django template tag", 914, 916, "{%"),
                new Classifcation("keyword", 917, 929, "endspaceless"),
                new Classifcation("Django template tag", 930, 932, "%}"),
                new Classifcation("Django template tag", 937, 939, "{%"),
                new Classifcation("keyword", 940, 943, "ssi"),
                new Classifcation("excluded code", 943, 957, " /home/foo.txt"),
                new Classifcation("Django template tag", 958, 960, "%}"),
                new Classifcation("Django template tag", 967, 969, "{%"),
                new Classifcation("keyword", 970, 982, "unknownblock"),
                new Classifcation("Django template tag", 983, 985, "%}"),
                new Classifcation("HTML Tag Delimiter", 989, 991, "</"),
                new Classifcation("HTML Element Name", 991, 995, "body"),
                new Classifcation("HTML Tag Delimiter", 995, 996, ">"),
                new Classifcation("HTML Tag Delimiter", 998, 1000, "</"),
                new Classifcation("HTML Element Name", 1000, 1004, "html"),
                new Classifcation("HTML Tag Delimiter", 1004, 1005, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion1() {
            InsertionTest("Insertion1.html.djt", 8, 10, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("Django template tag", 71, 73, "{{"),
                new Classifcation("identifier", 74, 78, "faoo"),
                new Classifcation("Django template tag", 79, 81, "}}"),
                new Classifcation("Django template tag", 85, 87, "{{"),
                new Classifcation("identifier", 88, 91, "foo"),
                new Classifcation("Django template tag", 92, 94, "}}"),
                new Classifcation("HTML Tag Delimiter", 96, 98, "</"),
                new Classifcation("HTML Element Name", 98, 102, "body"),
                new Classifcation("HTML Tag Delimiter", 102, 103, ">"),
                new Classifcation("HTML Tag Delimiter", 105, 107, "</"),
                new Classifcation("HTML Element Name", 107, 111, "html"),
                new Classifcation("HTML Tag Delimiter", 111, 112, ">")
            );

            InsertionTest("Insertion1.html.djt", 8, 10, "}aaa",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("Django template tag", 71, 73, "{{"),
                new Classifcation("identifier", 74, 78, "faoo"),
                new Classifcation("Django template tag", 79, 81, "}}"),
                new Classifcation("Django template tag", 88, 90, "{{"),
                new Classifcation("identifier", 91, 94, "foo"),
                new Classifcation("Django template tag", 95, 97, "}}"),
                new Classifcation("HTML Tag Delimiter", 99, 101, "</"),
                new Classifcation("HTML Element Name", 101, 105, "body"),
                new Classifcation("HTML Tag Delimiter", 105, 106, ">"),
                new Classifcation("HTML Tag Delimiter", 108, 110, "</"),
                new Classifcation("HTML Element Name", 110, 114, "html"),
                new Classifcation("HTML Tag Delimiter", 114, 115, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion2() {
            InsertionDeletionTest("Insertion2.html.djt", 9, 34, "{",
                new Classifcation[] {
                    new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                    new Classifcation("HTML Element Name", 1, 5, "html"),
                    new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                    new Classifcation("Django template tag", 8, 10, "{{"),
                    new Classifcation("Django template tag", 13, 15, "}}"),
                    new Classifcation("HTML Tag Delimiter", 17, 18, "<"),
                    new Classifcation("HTML Element Name", 18, 22, "head"),
                    new Classifcation("HTML Tag Delimiter", 22, 24, "><"),
                    new Classifcation("HTML Element Name", 24, 29, "title"),
                    new Classifcation("HTML Tag Delimiter", 29, 32, "></"),
                    new Classifcation("HTML Element Name", 32, 37, "title"),
                    new Classifcation("HTML Tag Delimiter", 37, 40, "></"),
                    new Classifcation("HTML Element Name", 40, 44, "head"),
                    new Classifcation("HTML Tag Delimiter", 44, 45, ">"),
                    new Classifcation("HTML Tag Delimiter", 49, 50, "<"),
                    new Classifcation("HTML Element Name", 50, 54, "body"),
                    new Classifcation("HTML Tag Delimiter", 54, 55, ">"),
                    new Classifcation("HTML Tag Delimiter", 57, 58, "<"),
                    new Classifcation("HTML Element Name", 58, 64, "script"),
                    new Classifcation("HTML Tag Delimiter", 64, 65, ">"),
                    new Classifcation("HTML Tag Delimiter", 67, 69, "</"),
                    new Classifcation("HTML Element Name", 69, 75, "script"),
                    new Classifcation("HTML Tag Delimiter", 75, 76, ">"),
                    new Classifcation("Django template tag", 96, 98, "{{"),
                    new Classifcation("identifier", 99, 103, "faoo"),
                    new Classifcation("Django template tag", 106, 108, "}}"),
                    new Classifcation("Django template tag", 113, 115, "{{"),
                    new Classifcation("identifier", 116, 119, "foo"),
                    new Classifcation("Django template tag", 120, 122, "}}"),
                    new Classifcation("HTML Tag Delimiter", 124, 126, "</"),
                    new Classifcation("HTML Element Name", 126, 130, "body"),
                    new Classifcation("HTML Tag Delimiter", 130, 131, ">"),
                    new Classifcation("HTML Tag Delimiter", 133, 135, "</"),
                    new Classifcation("HTML Element Name", 135, 139, "html"),
                    new Classifcation("HTML Tag Delimiter", 139, 140, ">")
                },
                new Classifcation[]     {
                    new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                    new Classifcation("HTML Element Name", 1, 5, "html"),
                    new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                    new Classifcation("Django template tag", 8, 10, "{{"),
                    new Classifcation("Django template tag", 13, 15, "}}"),
                    new Classifcation("HTML Tag Delimiter", 17, 18, "<"),
                    new Classifcation("HTML Element Name", 18, 22, "head"),
                    new Classifcation("HTML Tag Delimiter", 22, 24, "><"),
                    new Classifcation("HTML Element Name", 24, 29, "title"),
                    new Classifcation("HTML Tag Delimiter", 29, 32, "></"),
                    new Classifcation("HTML Element Name", 32, 37, "title"),
                    new Classifcation("HTML Tag Delimiter", 37, 40, "></"),
                    new Classifcation("HTML Element Name", 40, 44, "head"),
                    new Classifcation("HTML Tag Delimiter", 44, 45, ">"),
                    new Classifcation("HTML Tag Delimiter", 49, 50, "<"),
                    new Classifcation("HTML Element Name", 50, 54, "body"),
                    new Classifcation("HTML Tag Delimiter", 54, 55, ">"),
                    new Classifcation("HTML Tag Delimiter", 57, 58, "<"),
                    new Classifcation("HTML Element Name", 58, 64, "script"),
                    new Classifcation("HTML Tag Delimiter", 64, 65, ">"),
                    new Classifcation("HTML Tag Delimiter", 67, 69, "</"),
                    new Classifcation("HTML Element Name", 69, 75, "script"),
                    new Classifcation("HTML Tag Delimiter", 75, 76, ">"),
                    new Classifcation("Django template tag", 96, 98, "{{"),
                    new Classifcation("identifier", 99, 103, "faoo"),
                    new Classifcation("Django template tag", 106, 108, "}}"),
                    new Classifcation("HTML Tag Delimiter", 123, 125, "</"),
                    new Classifcation("HTML Element Name", 125, 129, "body"),
                    new Classifcation("HTML Tag Delimiter", 129, 130, ">"),
                    new Classifcation("HTML Tag Delimiter", 132, 134, "</"),
                    new Classifcation("HTML Element Name", 134, 138, "html"),
                    new Classifcation("HTML Tag Delimiter", 138, 139, ">")
                }
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion3() {
            InsertionTest("Insertion3.html.djt", 2, 5, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("Django template tag", 8, 10, "{{"),
                new Classifcation("Django template tag", 11, 13, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion4() {
            InsertionTest("Insertion4.html.djt", 1, 1, "{",
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 10, 12, "}}")
            );

            InsertionTest("Insertion4.html.djt", 1, 2, "{",
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 10, 12, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion5() {
            InsertionTest("Insertion5.html.djt", 1, 2, "#",
                new Classifcation("Django template tag", 0, 2, "{#"),
                new Classifcation("comment", 2, 11, "{<html>\r\n"),
                new Classifcation("Django template tag", 11, 13, "#}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion6() {
            InsertionTest("Insertion6.html.djt", 1, 4, "a",
                new Classifcation("Django template tag", 4, 6, "{{"),
                new Classifcation("Django template tag", 16, 18, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion7() {
            InsertionTest("Insertion7.html.djt", 1, 16, "{",
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 10, 12, "}}"),
                new Classifcation("Django template tag", 15, 17, "{{"),
                new Classifcation("Django template tag", 28, 30, "}}"),
                new Classifcation("HTML Tag Delimiter", 38, 39, "<"),
                new Classifcation("HTML Element Name", 39, 42, "foo"),
                new Classifcation("HTML Tag Delimiter", 42, 43, ">"),
                new Classifcation("Django template tag", 49, 51, "{{"),
                new Classifcation("Django template tag", 61, 63, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion8() {
            InsertionTest("Insertion8.html.djt", 2, 9, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("Django template tag", 8, 10, "{{"),
                new Classifcation("identifier", 11, 14, "foo"),
                new Classifcation("Django template tag", 15, 17, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion9() {
            InsertionTest("Insertion9.html.djt", 1, 7, "a",
                new Classifcation("Django template tag", 4, 6, "{{"),
                new Classifcation("identifier", 6, 7, "a"),
                new Classifcation("Django template tag", 17, 19, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion10() {
            InsertionTest("Insertion10.html.djt", 7, 10, "a",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("Django template tag", 72, 74, "{{"),
                new Classifcation("identifier", 75, 78, "foo"),
                new Classifcation("Django template tag", 79, 81, "}}"),
                new Classifcation("Django template tag", 84, 86, "{{"),
                new Classifcation("identifier", 87, 91, "faoo"),
                new Classifcation("Django template tag", 104, 106, "}}"),
                new Classifcation("HTML Tag Delimiter", 108, 110, "</"),
                new Classifcation("HTML Element Name", 110, 114, "body"),
                new Classifcation("HTML Tag Delimiter", 114, 115, ">"),
                new Classifcation("HTML Tag Delimiter", 117, 119, "</"),
                new Classifcation("HTML Element Name", 119, 123, "html"),
                new Classifcation("HTML Tag Delimiter", 123, 124, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion11() {
            InsertionTest("Insertion11.html.djt", 9, 5, 1, "{", true,
                new Classifcation("Django template tag", 2, 4, "{%"),
                new Classifcation("keyword", 5, 9, "load"),
                new Classifcation("Django template tag", 22, 24, "%}"),
                new Classifcation("Django template tag", 28, 30, "{%"),
                new Classifcation("keyword", 31, 33, "if"),
                new Classifcation("identifier", 34, 50, "latest_poll_list"),
                new Classifcation("Django template tag", 51, 53, "%}"),
                new Classifcation("HTML Tag Delimiter", 59, 60, "<"),
                new Classifcation("HTML Element Name", 60, 62, "ul"),
                new Classifcation("HTML Tag Delimiter", 62, 63, ">"),
                new Classifcation("Django template tag", 81, 83, "{%"),
                new Classifcation("keyword", 84, 87, "for"),
                new Classifcation("keyword", 99, 101, "in"),
                new Classifcation("Django template tag", 123, 125, "%}"),
                new Classifcation("Django template tag", 135, 137, "{%"),
                new Classifcation("keyword", 138, 141, "for"),
                new Classifcation("keyword", 153, 155, "in"),
                new Classifcation("Django template tag", 173, 175, "%}"),
                new Classifcation("Django template tag", 185, 187, "{%"),
                new Classifcation("keyword", 188, 194, "endfor"),
                new Classifcation("Django template tag", 195, 197, "%}"),
                new Classifcation("Django template tag", 203, 205, "{%"),
                new Classifcation("keyword", 206, 212, "endfor"),
                new Classifcation("Django template tag", 213, 215, "%}"),
                new Classifcation("HTML Tag Delimiter", 223, 225, "</"),
                new Classifcation("HTML Element Name", 225, 227, "ul"),
                new Classifcation("HTML Tag Delimiter", 227, 228, ">"),
                new Classifcation("Django template tag", 230, 232, "{%"),
                new Classifcation("keyword", 233, 237, "else"),
                new Classifcation("Django template tag", 238, 240, "%}"),
                new Classifcation("HTML Tag Delimiter", 246, 247, "<"),
                new Classifcation("HTML Element Name", 247, 248, "p"),
                new Classifcation("HTML Tag Delimiter", 248, 249, ">"),
                new Classifcation("HTML Tag Delimiter", 272, 274, "</"),
                new Classifcation("HTML Element Name", 274, 275, "p"),
                new Classifcation("HTML Tag Delimiter", 275, 276, ">"),
                new Classifcation("Django template tag", 282, 284, "{%"),
                new Classifcation("keyword", 285, 297, "current_time"),
                new Classifcation("excluded code", 297, 317, " \"%Y-%m-%d %I:%M %p\""),
                new Classifcation("Django template tag", 318, 320, "%}"),
                new Classifcation("Django template tag", 322, 324, "{%"),
                new Classifcation("keyword", 325, 330, "endif"),
                new Classifcation("Django template tag", 331, 333, "%}")

            );

            InsertionTest("Insertion11.html.djt", 9, 5, 2, "{%", true,
                new Classifcation("Django template tag", 2, 4, "{%"),
                new Classifcation("keyword", 5, 9, "load"),
                new Classifcation("Django template tag", 22, 24, "%}"),
                new Classifcation("Django template tag", 28, 30, "{%"),
                new Classifcation("keyword", 31, 33, "if"),
                new Classifcation("identifier", 34, 50, "latest_poll_list"),
                new Classifcation("Django template tag", 51, 53, "%}"),
                new Classifcation("HTML Tag Delimiter", 59, 60, "<"),
                new Classifcation("HTML Element Name", 60, 62, "ul"),
                new Classifcation("HTML Tag Delimiter", 62, 63, ">"),
                new Classifcation("Django template tag", 81, 83, "{%"),
                new Classifcation("keyword", 84, 87, "for"),
                new Classifcation("keyword", 99, 101, "in"),
                new Classifcation("Django template tag", 123, 125, "%}"),
                new Classifcation("Django template tag", 135, 137, "{%"),
                new Classifcation("keyword", 138, 141, "for"),
                new Classifcation("keyword", 153, 155, "in"),
                new Classifcation("Django template tag", 173, 175, "%}"),
                new Classifcation("Django template tag", 185, 187, "{%"),
                new Classifcation("keyword", 188, 194, "endfor"),
                new Classifcation("Django template tag", 195, 197, "%}"),
                new Classifcation("Django template tag", 203, 205, "{%"),
                new Classifcation("keyword", 206, 212, "endfor"),
                new Classifcation("Django template tag", 213, 215, "%}"),
                new Classifcation("HTML Tag Delimiter", 223, 225, "</"),
                new Classifcation("HTML Element Name", 225, 227, "ul"),
                new Classifcation("HTML Tag Delimiter", 227, 228, ">"),
                new Classifcation("Django template tag", 230, 232, "{%"),
                new Classifcation("keyword", 233, 237, "else"),
                new Classifcation("Django template tag", 238, 240, "%}"),
                new Classifcation("HTML Tag Delimiter", 246, 247, "<"),
                new Classifcation("HTML Element Name", 247, 248, "p"),
                new Classifcation("HTML Tag Delimiter", 248, 249, ">"),
                new Classifcation("HTML Tag Delimiter", 272, 274, "</"),
                new Classifcation("HTML Element Name", 274, 275, "p"),
                new Classifcation("HTML Tag Delimiter", 275, 276, ">"),
                new Classifcation("Django template tag", 282, 284, "{%"),
                new Classifcation("keyword", 285, 297, "current_time"),
                new Classifcation("excluded code", 297, 317, " \"%Y-%m-%d %I:%M %p\""),
                new Classifcation("Django template tag", 318, 320, "%}"),
                new Classifcation("Django template tag", 322, 324, "{%"),
                new Classifcation("keyword", 325, 330, "endif"),
                new Classifcation("Django template tag", 331, 333, "%}")
            );
        }


        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Deletion1() {
            DeletionTest("Deletion1.html.djt", 1, 2, 1,
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 12, 14, "}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 3, 1,
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 12, 14, "}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 4, 1,
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 12, 14, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Paste1() {
            PasteTest("Paste1.html.djt", 1, 2, "{{foo}}", "{{bazz}}",
                new Classifcation("Django template tag", 0, 2, "{{"),
                new Classifcation("Django template tag", 12, 14, "}}"),
                new Classifcation("HTML Tag Delimiter", 18, 19, "<"),
                new Classifcation("HTML Element Name", 19, 22, "foo"),
                new Classifcation("HTML Tag Delimiter", 22, 23, ">"),
                new Classifcation("Django template tag", 25, 27, "{{"),
                new Classifcation("identifier", 27, 31, "bazz"),
                new Classifcation("Django template tag", 31, 33, "}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed1() {
            SelectAllAndDeleteTest("SelectAllMixed1.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed2() {
            SelectAllAndDeleteTest("SelectAllMixed2.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed3() {
            SelectAllAndDeleteTest("SelectAllMixed3.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed4() {
            SelectAllAndDeleteTest("SelectAllMixed4.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllTag() {
            SelectAllAndDeleteTest("SelectAllTag.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllText() {
            SelectAllAndDeleteTest("SelectAllText.html.djt");
        }

        private static void SelectAllAndDeleteTest(string filename) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);

            item.Invoke(() => {
                using (var edit = item.TextView.TextBuffer.CreateEdit()) {
                    edit.Delete(new Span(0, item.TextView.TextBuffer.CurrentSnapshot.Length));
                    edit.Apply();
                }
            });

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(spans);
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void DeletionTest(string filename, int line, int column, int deletionCount, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            for (int i = 0; i < deletionCount; i++) {
                Keyboard.Backspace();
            }

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void PasteTest(string filename, int line, int column, string selectionText, string pasteText, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                System.Windows.Clipboard.SetText(pasteText);
            });

            AutoResetEvent are = new AutoResetEvent(false);
            EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                are.Set();
            };
            item.TextView.TextBuffer.Changed += textChangedHandler;
            Keyboard.ControlV();
            Assert.IsTrue(are.WaitOne(5000));
            item.TextView.TextBuffer.Changed -= textChangedHandler;

            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                var classifier = item.Classifier;
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });
            Assert.IsNotNull(spans);
            EditorTests.VerifyClassification(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void InsertionTest(string filename, int line, int column, string insertionText, params Classifcation[] expected) {
            InsertionTest(filename, line, column, -1, insertionText, false, expected);
        }

        private static void InsertionTest(string filename, int line, int column, int selectionLength, string insertionText, bool paste, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            var pos = item.TextView.Caret.Position.BufferPosition.Position;
            if (selectionLength != -1) {
                item.Select(line, column, selectionLength);
            }
            
            if (!String.IsNullOrEmpty(insertionText)) {
                AutoResetEvent are = new AutoResetEvent(false);
                int delta = 0;
                EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                    foreach (var change in args.Changes) {
                        delta += change.Delta;
                    }
                    if (selectionLength == -1) {
                        if (delta == insertionText.Length) {
                            are.Set();
                        }
                    } else {
                        if (delta == insertionText.Length - selectionLength) {
                            are.Set();
                        }
                    }
                };

                item.TextView.TextBuffer.Changed += textChangedHandler;
                if (paste) {
                    item.Invoke(() => System.Windows.Clipboard.SetText(insertionText));
                    Keyboard.ControlV();
                } else {
                    Keyboard.Type(insertionText);
                }
                Assert.IsTrue(are.WaitOne(5000));

                var newPos = item.TextView.Caret.Position.BufferPosition;
                Assert.AreEqual(pos + insertionText.Length, newPos.Position);
                item.TextView.TextBuffer.Changed -= textChangedHandler;
            }

            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                var classifier = item.Classifier;
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });
            
            Assert.IsNotNull(spans);
            EditorTests.VerifyClassification(
                spans,
                expected
            );

            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void InsertionDeletionTest(string filename, int line, int column, string insertionText, Classifcation[] expectedFirst, Classifcation[] expectedAfter) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            AutoResetEvent are = new AutoResetEvent(false);
            EventHandler<TextContentChangedEventArgs> textChangedHandler = (sender, args) => {
                are.Set();
            };

            item.TextView.TextBuffer.Changed += textChangedHandler;
            Keyboard.Type(insertionText);
            Assert.IsTrue(are.WaitOne(5000));
            are.Reset();

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            IList<ClassificationSpan> spans = null;
            item.Invoke(() => {
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });
            Assert.IsNotNull(spans);
            EditorTests.VerifyClassification(
                spans,
                expectedFirst
            );

            for (int i = 0; i < insertionText.Length; i++) {
                Keyboard.Backspace();
            }
            Assert.IsTrue(are.WaitOne(5000));
            item.TextView.TextBuffer.Changed -= textChangedHandler;

            item.Invoke(() => {
                snapshot = item.TextView.TextBuffer.CurrentSnapshot;
                spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            });

            EditorTests.VerifyClassification(
                spans,
                expectedAfter
            );
            
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static EditorWindow OpenDjangoProjectItem(string startItem, out Window window) {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\DjangoEditProject.sln", startItem);

            var item = project.ProjectItems.Item(startItem);
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);

            return doc;
        }
    }
}