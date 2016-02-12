﻿'MultipleObjectTrackingVB.sln
'frmMain.vb
'
'form components
'
'tableLayoutPanel
'btnOpenFile
'lblChosenFile
'imageBox
'txtInfo
'openFileDialog
'
'Emgu CV 3.1.0

Option Explicit On      'require explicit declaration of variables, this is NOT Python !!
Option Strict On        'restrict implicit data type conversions to only widening conversions

Imports Emgu.CV                 '
Imports Emgu.CV.CvEnum          'usual Emgu Cv imports
Imports Emgu.CV.Structure       '
Imports Emgu.CV.UI              '
Imports Emgu.CV.Util

'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Public Class frmMain

    ' member variables ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Dim SCALAR_BLACK As New MCvScalar(0.0, 0.0, 0.0)
    Dim SCALAR_WHITE As New MCvScalar(255.0, 255.0, 255.0)
    Dim SCALAR_BLUE As New MCvScalar(255.0, 0.0, 0.0)
    Dim SCALAR_GREEN As New MCvScalar(0.0, 200.0, 0.0)
    Dim SCALAR_RED As New MCvScalar(0.0, 0.0, 255.0)

    Dim capVideo As Capture
    
    Dim blnFormClosing As Boolean = False

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub frmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        blnFormClosing = True
        CvInvoke.DestroyAllWindows()
    End Sub
    
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub btnOpenFile_Click(sender As Object, e As EventArgs) Handles btnOpenFile.Click

        Dim drChosenFile As DialogResult

        drChosenFile = openFileDialog.ShowDialog()                 'open file dialog

        If (drChosenFile <> DialogResult.OK Or openFileDialog.FileName = "") Then    'if user chose Cancel or filename is blank . . .
            lblChosenFile.Text = "file not chosen"              'show error message on label
            Return                                              'and exit function
        End If

        Try
            capVideo = New Capture(openFileDialog.FileName)        'attempt to open chosen video file
        Catch ex As Exception                                   'catch error if unsuccessful
                                                                'show error via message box
            MessageBox.Show("unable to read video file, error: " + ex.Message)
            Return
        End Try

        lblChosenFile.Text = openFileDialog.FileName

        If (capVideo Is Nothing) Then
            txtInfo.AppendText("unable to read video file")
        End If

        If (capVideo.GetCaptureProperty(CapProp.FrameCount) < 2) Then               'check and make sure the video has at least 2 frames
            txtInfo.AppendText("error: video file must have at least two frames")
        End If

        trackBlobsAndUpdateGUI()
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub trackBlobsAndUpdateGUI()

        Dim imgFrame1 As Mat
        Dim imgFrame2 As Mat

        Dim blobs As New List(Of Blob)

        Dim blnFirstFrame As Boolean = True

        imgFrame1 = capVideo.QueryFrame()
        imgFrame2 = capVideo.QueryFrame()

        While (blnFormClosing = False)

            Dim currentFrameBlobs As New List(Of Blob)

            Dim imgFrame1Copy As Mat = imgFrame1.Clone()
            Dim imgFrame2Copy As Mat = imgFrame2.Clone()

            Dim imgDifference As New Mat(imgFrame1.Size, DepthType.Cv8U, 1)
            Dim imgThresh As New Mat(imgFrame1.Size, DepthType.Cv8U, 1)

            CvInvoke.CvtColor(imgFrame1Copy, imgFrame1Copy, ColorConversion.Bgr2Gray)
            CvInvoke.CvtColor(imgFrame2Copy, imgFrame2Copy, ColorConversion.Bgr2Gray)

            CvInvoke.GaussianBlur(imgFrame1Copy, imgFrame1Copy, New Size(5, 5), 0)
            CvInvoke.GaussianBlur(imgFrame2Copy, imgFrame2Copy, New Size(5, 5), 0)

            CvInvoke.AbsDiff(imgFrame1Copy, imgFrame2Copy, imgDifference)

            CvInvoke.Threshold(imgDifference, imgThresh, 30, 255.0, ThresholdType.Binary)

            CvInvoke.Imshow("imgThresh", imgThresh)

            Dim structuringElement3x3 As Mat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, New Size(3, 3), New Point(-1, -1))
            Dim structuringElement5x5 As Mat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, New Size(5, 5), New Point(-1, -1))
            Dim structuringElement7x7 As Mat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, New Size(7, 7), New Point(-1, -1))
            Dim structuringElement9x9 As Mat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, New Size(9, 9), New Point(-1, -1))

            CvInvoke.Dilate(imgThresh, imgThresh, structuringElement7x7, New Point(-1, -1), 1, BorderType.Default, New MCvScalar(0, 0, 0))
            CvInvoke.Erode(imgThresh, imgThresh, structuringElement3x3, New Point(-1, -1), 1, BorderType.Default, New MCvScalar(0, 0, 0))

            Dim imgThreshCopy As Mat = imgThresh.Clone()

            Dim contours As New VectorOfVectorOfPoint()

            CvInvoke.FindContours(imgThreshCopy, contours, Nothing, RetrType.External, ChainApproxMethod.ChainApproxSimple)

            For i As Integer = 0 To contours.Size() - 1

                Dim possibleBlob As New Blob(contours(i))

                If (possibleBlob.intCurrentRectArea > 100 And _
                    possibleBlob.dblCurrentAspectRatio >= 0.2 And _
                    possibleBlob.dblCurrentAspectRatio <= 1.25 And _
                    possibleBlob.currentBoundingRect.Width > 20 And _
                    possibleBlob.currentBoundingRect.Height > 20 And _
                    possibleBlob.dblCurrentDiagonalSize > 30.0) Then
                    currentFrameBlobs.Add(possibleBlob)
                End If
                
            Next
            
            If (blnFirstFrame = True) Then
                For Each currentFrameBlob As Blob In currentFrameBlobs
                    blobs.Add(currentFrameBlob)
                Next
            Else
                matchCurrentFrameBlobsToExistingBlobs(blobs, currentFrameBlobs)
            End If

            Dim imgContours As New Mat(imgFrame1.Size, DepthType.Cv8U, 3)

            contours = New VectorOfVectorOfPoint()

            For Each blob As Blob In blobs
                If (blob.blnStillBeingTracked = True) Then
                    contours.Push(blob.currentContour)
                End If
            Next
            
            CvInvoke.DrawContours(imgContours, contours, -1, SCALAR_WHITE, -1)

            CvInvoke.Imshow("imgContours", imgContours)

            imgFrame2Copy = imgFrame2.Clone()

            drawBlobInfoOnImage(blobs, imgFrame2Copy)

            imageBox.Image = imgFrame2Copy

                    'now we prepare for the next iteration

            currentFrameBlobs.Clear()

            imgFrame1 = imgFrame2.Clone()                   'move frame 1 up to where frame 2 is

            If (capVideo.GetCaptureProperty(CapProp.PosFrames) + 1 < capVideo.GetCaptureProperty(CapProp.FrameCount)) Then      'if there is at least one more frame
                imgFrame2 = capVideo.QueryFrame()               'get the next frame
            Else                                                'else if there is not at least one more frame
                txtInfo.AppendText("end of video")              'show end of video message
                Exit While                                      'and jump out of while loop
            End If
            
            blnFirstFrame = False

            Application.DoEvents()
            
        End While
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub matchCurrentFrameBlobsToExistingBlobs(ByRef existingBlobs As List(Of Blob), ByRef currentFrameBlobs As List(Of Blob))

        For Each existingBlob As Blob In existingBlobs
            existingBlob.blnCurrentMatchFoundOrNewBlob = False
            existingBlob.predictNextPosition()
        Next

        For Each currentFrameBlob As Blob In currentFrameBlobs

            Dim intIndexOfLeastDistance As Integer = 0
            Dim dblLeastDistance As Double = 1000000.0

            For i As Integer = 0 To existingBlobs.Count() - 1

                If (existingBlobs(i).blnStillBeingTracked = True) Then

                    Dim dblDistance As Double = distanceBetweenPoints(currentFrameBlob.centerPositions.Last(), existingBlobs(i).predictedNextPosition)

                    If (dblDistance < dblLeastDistance) Then
                        dblLeastDistance = dblDistance
                        intIndexOfLeastDistance = i
                    End If
                    
                End If
                
            Next

            If (dblLeastDistance < currentFrameBlob.dblCurrentDiagonalSize * 1.5) Then
                addBlobToExistingBlobs(currentFrameBlob, existingBlobs, intIndexOfLeastDistance)
            Else
                addNewBlob(currentFrameBlob, existingBlobs)
            End If

        Next

        For Each existingBlob As Blob In existingBlobs

            If (existingBlob.blnCurrentMatchFoundOrNewBlob = False) Then
                existingBlob.intNumOfConsecutiveFramesWithoutAMatch = existingBlob.intNumOfConsecutiveFramesWithoutAMatch + 1
            End If

            If (existingBlob.intNumOfConsecutiveFramesWithoutAMatch >= 5) Then
                existingBlob.blnStillBeingTracked = False
            End If

        Next
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub addBlobToExistingBlobs(ByRef currentFrameBlob As Blob, ByRef existingBlobs As List(Of Blob), ByRef intIndex As Integer)

        existingBlobs(intIndex).currentContour = currentFrameBlob.currentContour
        existingBlobs(intIndex).currentBoundingRect = currentFrameBlob.currentBoundingRect
        
        existingBlobs(intIndex).centerPositions.Add(currentFrameBlob.centerPositions.Last())
        
        existingBlobs(intIndex).dblCurrentDiagonalSize = currentFrameBlob.dblCurrentDiagonalSize
        existingBlobs(intIndex).dblCurrentAspectRatio = currentFrameBlob.dblCurrentAspectRatio
        
        existingBlobs(intIndex).blnStillBeingTracked = True
        existingBlobs(intIndex).blnCurrentMatchFoundOrNewBlob = True
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub addNewBlob(ByRef currentFrameBlob As Blob, ByRef existingBlobs As List(Of Blob))

        currentFrameBlob.blnCurrentMatchFoundOrNewBlob = True

        existingBlobs.Add(currentFrameBlob)

    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Function distanceBetweenPoints(point1 As Point, point2 As Point) As Double

        Dim intX As Integer = Math.Abs(point1.X - point2.X)
        Dim intY As Integer = Math.Abs(point1.Y - point2.Y)

        Return Math.Sqrt((intX ^ 2) + (intY ^ 2))

    End Function
    
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub drawBlobInfoOnImage(ByRef blobs As List(Of Blob), ByRef imgFrame2Copy As Mat)

        For i As Integer = 0 To blobs.Count - 1

            If (blobs(i).blnStillBeingTracked = True) Then

                CvInvoke.Rectangle(imgFrame2Copy, blobs(i).currentBoundingRect, SCALAR_RED, 2)

                Dim fontFace As FontFace = FontFace.HersheySimplex
                Dim dblFontScale As Double = blobs(i).dblCurrentDiagonalSize / 60.0
                Dim intFontThickness As Integer = CInt(Math.Round(dblFontScale * 1.0))

                CvInvoke.PutText(imgFrame2Copy, i.ToString(), blobs(i).centerPositions.Last(), fontFace, dblFontScale, SCALAR_GREEN, intFontThickness)
                
            End If
            
        Next

    End Sub

End Class



