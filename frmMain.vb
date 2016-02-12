'ObjectTrackingVB.sln
'frmMain.vb
'
'Emgu CV 3.0.0
'
'add the following components to your form:
'
'tableLayoutPanel (TableLayoutPanel)
'btnOpenFile (Button)
'lblChosenFile (Label)
'cbShowSteps (CheckBox)
'ibOriginal (Emgu ImageBox)
'txtInfo (TextBox)
'ofdOpenFile (OpenFileDialog)

Option Explicit On      'require explicit declaration of variables, this is NOT Python !!
Option Strict On        'restrict implicit data type conversions to only widening conversions

Imports Emgu.CV                 'usual Emgu Cv imports
Imports Emgu.CV.CvEnum          '
Imports Emgu.CV.Structure       '
Imports Emgu.CV.UI              '
Imports Emgu.CV.Util            '

'''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
Public Class frmMain

    ' member variables ''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Dim SCALAR_WHITE As New MCvScalar(255.0, 255.0, 255.0)
    Dim SCALAR_BLACK As New MCvScalar(0.0, 0.0, 0.0)

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
    Private Sub frmMain_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Private Sub btnOpenFile_Click(sender As Object, e As EventArgs) Handles btnOpenFile.Click
        Dim drChosenFile As DialogResult

        drChosenFile = ofdOpenFile.ShowDialog()                 'open file dialog
        
        If (drChosenFile <> DialogResult.OK Or ofdOpenFile.FileName = "") Then    'if user chose Cancel or filename is blank . . .
            lblChosenFile.Text = "file not chosen"              'show error message on label
            Return                                              'and exit function
        End If
        
        Try
            capVideo = New Capture(ofdOpenFile.FileName)        'attempt to open chosen video file
        Catch ex As Exception                                   'catch error if unsuccessful
                                                                'show error via message box
            txtInfo.AppendText("unable to read video file, error: " + ex.Message)
            Return
        End Try

        If (capVideo Is Nothing) Then
            txtInfo.AppendText("unable to read video file")
        End If

        If (capVideo.GetCaptureProperty(CapProp.FrameCount) < 2) Then               'check and make sure the video has at least 2 frames
            txtInfo.AppendText("error: video file must have at least two frames")
        End If

        trackObjects()
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub trackObjects()

        'if we get here, we have already checked the video file was opened successfully and has at least two frames

        Dim imgFrame1 As Mat = capVideo.QueryFrame()
        Dim imgFrame2 As Mat = capVideo.QueryFrame()

        Dim blobs As New List(Of Blob)

        Dim blnFirstFrame As Boolean = True
        
        While(True)

            Dim currentFrameBlobs As New List(Of Blob)

            If (blnFormClosing = True) Then
                Exit While
            End If

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
            Dim structuringElement15x15 As Mat = CvInvoke.GetStructuringElement(ElementShape.Rectangle, New Size(15, 15), New Point(-1, -1))
            
            For i As Integer = 0 To 2
                CvInvoke.Dilate(imgThresh, imgThresh, structuringElement5x5, New Point(-1, -1), 1, BorderType.Default, New MCvScalar(0, 0, 0))
                CvInvoke.Dilate(imgThresh, imgThresh, structuringElement5x5, New Point(-1, -1), 1, BorderType.Default, New MCvScalar(0, 0, 0))
                CvInvoke.Erode(imgThresh, imgThresh, structuringElement5x5, New Point(-1, -1), 1, BorderType.Default, New MCvScalar(0, 0, 0))
            Next

            Dim imgThreshCopy As Mat = imgThresh.Clone()
            
            Dim contours As New VectorOfVectorOfPoint()

            CvInvoke.FindContours(imgThreshCopy, contours, Nothing, RetrType.External, ChainApproxMethod.ChainApproxSimple)

            For i As Integer = 0 To contours.Size() - 1

                Dim possibleBlob As New Blob(contours(i))

                If (possibleBlob.intRectArea > 500 And _
                    possibleBlob.dblAspectRatio > 0.25 And _
                    possibleBlob.dblAspectRatio < 4.0 And _
                    possibleBlob.boundingRect.Width > 30 And _
                    possibleBlob.boundingRect.Height > 30 And _
                    possibleBlob.dblDiagonalSize > 60.0) Then
                    currentFrameBlobs.Add(possibleBlob)
                End If
            Next
            
            If (blnFirstFrame = True) Then                  'if we're on the first frame then there are no blobs to match to
                For Each currentFrameBlob As Blob In currentFrameBlobs                              'so add every blob to blobs
                    currentFrameBlob.listOfAllActualPoints.Add(currentFrameBlob.ptCurrentCenter)
                    blobs.Add(currentFrameBlob)
                Next
            Else                                            'else if we're past the first frame
                matchBlobs(blobs, currentFrameBlobs)        'match current blob to list of blobs
            End If
            
            Dim imgContours As New Mat(imgThresh.Size(), DepthType.Cv8U, 3)
            contours = New VectorOfVectorOfPoint()                  're-instaintate contours to clear it, for some reason contours.Clear() does not seem to work in EMGU CV
            
            For Each blob As Blob In blobs
                If (blob.blnStillBeingTracked) Then
                    contours.Push(blob.contour)
                End If
            Next

            CvInvoke.DrawContours(imgContours, contours, -1, SCALAR_WHITE, -1)
            
            CvInvoke.Imshow("imgContours", imgContours)
            
            imgFrame2Copy = imgFrame2.Clone()           'get another copy of frame 2 since we changed the previous frame 2 copy in the processing above

            drawBlobInfoOnImage(blobs, imgFrame2Copy)           '

            ibOriginal.Image = imgFrame2Copy                    'update image box on form
            
                    'now we prepare for the next iteration

            currentFrameBlobs.Clear()

            imgFrame1 = imgFrame2.Clone()           'move frame 1 up to where frame 2 is

            If ((capVideo.GetCaptureProperty(CapProp.PosFrames) + 1) < capVideo.GetCaptureProperty(CapProp.FrameCount)) Then    'if there is at least one more frame to read
                imgFrame2 = capVideo.QueryFrame()       'read the next frame into frame 2
            Else                                        'if there is not at least one more frame
                txtInfo.AppendText("end of video")
                Exit While
            End If

            blnFirstFrame = False
                                    'DoEvents is necessary to get the operating system to
            Application.DoEvents()  're-draw the image on the form, and to respond to closing the program if that was chosen
            
        End While
        
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub matchBlobs(ByRef existingBlobs As List(Of Blob), ByRef currentFrameBlobs As List(Of Blob))

        For Each existingBlob As Blob In existingBlobs
            existingBlob.blnCurrentMatchFoundOrNewBlob = False
        Next
        
        For Each currentFrameBlob As Blob In currentFrameBlobs
            
            Dim intIndexOfLeastDistance As Integer = 0
            Dim dblLeastDistance As Double = 1000000.0      'initialize least distance to a value higher than distance between blobs could ever possibly be
            
            For i As Integer = 0 To existingBlobs.Count - 1
                If (existingBlobs(i).blnStillBeingTracked = True) Then
                    Dim dblDistance As Double = distanceBetweenBlobs(currentFrameBlob, existingBlobs(i))

                    If (dblDistance < dblLeastDistance) Then
                        dblLeastDistance = dblDistance
                        intIndexOfLeastDistance = i
                    End If
                End If
            Next

            If (dblLeastDistance < currentFrameBlob.dblDiagonalSize * 1.5) Then             'if we found a match
                addBlobToExistingBlobs(currentFrameBlob, existingBlobs, intIndexOfLeastDistance)
            Else
                addNewBlob(currentFrameBlob, existingBlobs)
            End If
            
        Next

        For Each existingBlob As Blob In existingBlobs
            If (existingBlob.blnCurrentMatchFoundOrNewBlob = False) Then
                existingBlob.blnStillBeingTracked = False
            End If
        Next
        
    End Sub
    
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub addBlobToExistingBlobs(ByRef currentFrameBlob As Blob, ByRef existingBlobs As List(Of Blob), ByRef intIndex As Integer)

        existingBlobs(intIndex).contour = currentFrameBlob.contour
        existingBlobs(intIndex).boundingRect = currentFrameBlob.boundingRect
        existingBlobs(intIndex).ptCurrentCenter = currentFrameBlob.ptCurrentCenter
        existingBlobs(intIndex).dblDiagonalSize = currentFrameBlob.dblDiagonalSize
        existingBlobs(intIndex).dblAspectRatio = currentFrameBlob.dblAspectRatio
        existingBlobs(intIndex).intRectArea = currentFrameBlob.intRectArea

        existingBlobs(intIndex).listOfAllActualPoints.Add(currentFrameBlob.ptCurrentCenter)

        existingBlobs(intIndex).blnStillBeingTracked = True
        existingBlobs(intIndex).blnCurrentMatchFoundOrNewBlob = True
    End Sub

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub addNewBlob(ByRef currentFrameBlob As Blob, ByRef existingBlobs As List(Of Blob))

        currentFrameBlob.listOfAllActualPoints.Add(currentFrameBlob.ptCurrentCenter)
        currentFrameBlob.blnCurrentMatchFoundOrNewBlob = True

        existingBlobs.Add(currentFrameBlob)
    End Sub
    
    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    'use Pythagorean theorem to calculate distance between two blobs
    Function distanceBetweenBlobs(firstBlob As Blob, secondBlob As Blob) As Double
        Dim intX As Integer = Math.Abs(firstBlob.ptCurrentCenter.X - secondBlob.ptCurrentCenter.X)
        Dim intY As Integer = Math.Abs(firstBlob.ptCurrentCenter.Y - secondBlob.ptCurrentCenter.Y)

        Return Math.Sqrt((intX ^ 2) + (intY ^ 2))
    End Function

    '''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''''
    Sub drawBlobInfoOnImage(ByRef blobs As List(Of Blob), ByRef imgFrame2Copy As Mat)
        
        For i As Integer = 0 To blobs.Count - 1

            If (blobs(i).blnStillBeingTracked) Then

                CvInvoke.Rectangle(imgFrame2Copy, blobs(i).boundingRect, SCALAR_RED, 2)

                Dim fontFace As FontFace = FontFace.HersheySimplex
                Dim dblFontScale As Double = blobs(i).dblDiagonalSize / 60.0
                Dim intFontThickness As Integer = CInt(Math.Round(dblFontScale * 1.0))
            
                CvInvoke.PutText(imgFrame2Copy, i.ToString(), blobs(i).ptCurrentCenter, fontFace, dblFontScale, SCALAR_GREEN, intFontThickness)
                
            End If
            
        Next
        
    End Sub
    
End Class
























