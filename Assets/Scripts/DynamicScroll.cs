using System;
using UnityEngine;
using UnityEngine.UI;
using pooling;

namespace dynamicscroll
{
    public class DynamicScroll<T, T1> 
        where T : class
        where T1 : DynamicScrollObject<T>
    {
        public const float CONTENT_OFFSET_FIXER_LIMIT = 1000f;
        public float spacing = 15f;
        public readonly Pooling<T1> objectPool = new Pooling<T1>();
        public Action<Vector2> OnDragEvent;

        private ScrollRect mScrollRect;
        private VerticalLayoutGroup mVerticalLayoutGroup;
        private HorizontalLayoutGroup mHorizontalLayoutGroup;
        private GridLayoutGroup mGridLayoutGroup;
        private ContentSizeFitter mContentSizeFitter;

        private bool mIsVertical = false;
        private bool mIsHorizontal = false;
            
        private Vector2 mNewAnchoredPosition = Vector2.zero;
		private Vector2 mScrollVelocity = Vector2.zero;
		private Vector2 mLastPos = Vector2.zero;
		private T[] infoList;

        public void Initiate(ScrollRect scrollRect, T[] infoList, int startIndex, GameObject objReference, bool createMoreIfNeeded = true, int? forceAmount = null)
        {
            mScrollRect = scrollRect;
            if (mScrollRect == null)
                throw new Exception("No scroll rect in gameObject.");

            if (objReference == null)
                throw new Exception("No Reference GameObject setted.");

            this.infoList = infoList;
            
            mScrollRect.onValueChanged.AddListener(OnScroll);
            mScrollRect.movementType = ScrollRect.MovementType.Unrestricted;

            if (mScrollRect.content.GetComponent<VerticalLayoutGroup>() != null)
            {
                mVerticalLayoutGroup = mScrollRect.content.GetComponent<VerticalLayoutGroup>();
                mVerticalLayoutGroup.spacing = spacing;
            }

            if (mScrollRect.content.GetComponent<HorizontalLayoutGroup>() != null)
            {
                mHorizontalLayoutGroup = mScrollRect.content.GetComponent<HorizontalLayoutGroup>();
                mHorizontalLayoutGroup.spacing = spacing;
            }

            if (mScrollRect.content.GetComponent<GridLayoutGroup>() != null)
            {
                mGridLayoutGroup = mScrollRect.content.GetComponent<GridLayoutGroup>();
                mGridLayoutGroup.spacing = new Vector2(spacing, spacing);
            }

            if (mScrollRect.content.GetComponent<ContentSizeFitter>() != null)
                mContentSizeFitter = mScrollRect.content.GetComponent<ContentSizeFitter>();
         
            mIsHorizontal = mScrollRect.horizontal;
            mIsVertical = mScrollRect.vertical;

            objectPool.createMoreIfNeeded = createMoreIfNeeded;
			objectPool.Initialize(forceAmount.HasValue ? forceAmount.Value : 0, objReference, mScrollRect.content);

			CreateList(startIndex);
            
			DisableGridComponents();

            objectPool.ForEach(x => x.SetRefreshListAction(RefreshPosition));

            if (!mIsHorizontal || !mIsVertical) return;
            Debug.LogError("DynamicScroll doesn't support scrolling in both directions, please choose one direction (horizontal or vertical)");
            mIsHorizontal = false;
        }

        //if startIndex = -1, it will keep the same position
		public void ChangeList(T[] infoList, int startIndex = -1, bool resetContentPosition = false)
        {
            if (startIndex == -1)
                startIndex = GetHighest().currentIndex;

            mScrollRect.StopMovement();
			mScrollRect.content.anchoredPosition = Vector2.zero;

            var objs = objectPool.GetAllWithState(true);
            objs.ForEach(x => objectPool.Release(x));
            if(resetContentPosition)
                mScrollRect.content.anchoredPosition = new Vector2((mIsHorizontal ? spacing : 0), (mIsVertical ? spacing : 0));
            
            this.infoList = infoList;

			CreateList(startIndex);
        }

        private void CreateList(int startIndex)
		{
			float totalSize = 0f;
            var lastObjectPosition = Vector2.zero;
			var currentIndex = startIndex;
		    bool canDrag = false;

            if (infoList != null && infoList.Length > 0)
            {
                do
                {
                    var obj = objectPool.Collect();
                    obj.updateScrollObject(this.infoList[currentIndex], currentIndex);
                    var posX = currentIndex > 0 ? lastObjectPosition.x + (mIsHorizontal ? spacing : 0) : 0;
                    var posY = currentIndex > 0 ? lastObjectPosition.y - (mIsVertical ? spacing : 0) : 0;
                    obj.GetComponent<RectTransform>().anchoredPosition = new Vector2(posX, posY);
                    lastObjectPosition = new Vector2(posX + (mIsHorizontal ? obj.currentWidth : 0), posY - (mIsVertical ? obj.currentHeight : 0));

                    totalSize += (mIsVertical) ? obj.currentHeight : obj.currentWidth;
                    currentIndex++;
                } while (currentIndex < infoList.Length &&
                         (mIsVertical && totalSize < (mScrollRect.content.rect.height * 2f)) ||
                         (mIsHorizontal && totalSize < (mScrollRect.content.rect.width * 2f)));

                totalSize = (totalSize / (float)(currentIndex - startIndex)) * infoList.Length;
                canDrag = (mIsHorizontal && totalSize > mScrollRect.content.rect.width) || (mIsVertical && totalSize > mScrollRect.content.rect.height);
            }

            ToggleScroll(canDrag);
        }

        public void RefreshPosition()
        {
            var lastObject = GetHighest();
            var objs = objectPool.GetAllWithState(true);
            var index = lastObject.currentIndex;
			float totalSize = 0f;

            for (var i = 0; i < objs.Count; i++)
            {
                var currentObject = objectPool.Find(x => x.currentIndex == index);
                if (currentObject != null && currentObject.isUsing && currentObject.CompareTo(lastObject) != 0)
                {
                    var no = currentObject.GetComponent<RectTransform>();
                    var lo = lastObject.GetComponent<RectTransform>();
                    var x = (mIsHorizontal ? lo.anchoredPosition.x + lastObject.currentWidth + spacing : no.anchoredPosition.x);
                    var y = (mIsVertical ? lo.anchoredPosition.y - lastObject.currentHeight - spacing : no.anchoredPosition.y);
                    no.anchoredPosition = new Vector2(x, y);
					totalSize += mIsHorizontal ? lastObject.currentWidth : lastObject.currentHeight;
                    lastObject = currentObject;
                }

                index++;
            }

            if (lastObject != null)
                totalSize += mIsHorizontal ? lastObject.currentWidth : lastObject.currentHeight;

            bool canDrag = (mIsHorizontal && totalSize > mScrollRect.viewport.rect.width) || (mIsVertical && totalSize > mScrollRect.viewport.rect.height);
			ToggleScroll(canDrag);
        }

        public void ToggleScroll(bool active)
        {
            mScrollRect.enabled = active;
            mScrollRect.viewport.anchorMin = new Vector2(0, 0);
            mScrollRect.viewport.anchorMax = new Vector2(1, 1);
            mScrollRect.viewport.offsetMin = new Vector2(0, 0);
            mScrollRect.viewport.offsetMax = new Vector2(0, 0);
            mScrollRect.viewport.pivot = new Vector2(0.5f, 0.5f);

            if (!active)
				mScrollRect.content.anchoredPosition = Vector2.zero;
        }

        private void DisableGridComponents()
        {
            if (mVerticalLayoutGroup != null)
                mVerticalLayoutGroup.enabled = false;

            if (mHorizontalLayoutGroup != null)
                mHorizontalLayoutGroup.enabled = false;

            if (mContentSizeFitter != null)
                mContentSizeFitter.enabled = false;

            if (mGridLayoutGroup != null)
                mGridLayoutGroup.enabled = false;
        }

        private void OnScroll(Vector2 pos)
        {
			mScrollVelocity = mScrollRect.content.anchoredPosition - mLastPos;
            mLastPos = mScrollRect.content.anchoredPosition;
            
			if (LimitScroll()) return;

			if(OnDragEvent != null)
                OnDragEvent.Invoke(mScrollVelocity);

			//TODO: fix offset
			//ApplyOffsetIfNeeded();         

			var lowestObj = GetLowest();
			var lowestRect = lowestObj.GetComponent<RectTransform>();
			var highestObj = GetHighest();
			var highestRect = highestObj.GetComponent<RectTransform>();

			if(mIsHorizontal)
			{
				if(mScrollVelocity.x > 0)
				{
					var objPosX = highestRect.anchoredPosition.x + mScrollRect.content.anchoredPosition.x;
					if(objPosX > mScrollRect.content.rect.width + highestObj.currentWidth * 0.1f)
					{
						var nextIndex = lowestObj.currentIndex - 1;
						if (nextIndex < 0) return;
						objectPool.Release(highestObj);
						var obj = objectPool.Collect();
						obj.updateScrollObject(infoList[nextIndex], nextIndex);
                        obj.transform.SetAsFirstSibling();

						mNewAnchoredPosition = lowestRect.anchoredPosition;
						mNewAnchoredPosition.x += -lowestObj.currentWidth - spacing;

						obj.GetComponent<RectTransform>().anchoredPosition = mNewAnchoredPosition;
					}
				}
				else if(mScrollVelocity.x < 0)
				{
					var objPosX = lowestRect.anchoredPosition.x + mScrollRect.content.anchoredPosition.x;
					if(objPosX < lowestObj.currentWidth * -1.1f)
					{
						var nextIndex = highestObj.currentIndex + 1;
						if (nextIndex >= infoList.Length) return;
						objectPool.Release(lowestObj);                  
                        var obj = objectPool.Collect();
                        obj.updateScrollObject(infoList[nextIndex], nextIndex);
                        obj.transform.SetAsFirstSibling();

						mNewAnchoredPosition = highestRect.anchoredPosition;
						mNewAnchoredPosition.x += obj.currentWidth + spacing;

						obj.GetComponent<RectTransform>().anchoredPosition = mNewAnchoredPosition;
					}
				}
			}
			else if(mIsVertical)
			{
			    if (mScrollVelocity.y > 0)
                {
					var objPosY = highestRect.anchoredPosition.y + mScrollRect.content.anchoredPosition.y;
					if(objPosY - highestObj.currentHeight > highestObj.currentHeight * 0.1f)
					{
						var nextIndex = lowestObj.currentIndex + 1;
						if (nextIndex >= infoList.Length) return;
						objectPool.Release(highestObj);
                        var obj = objectPool.Collect();
						obj.updateScrollObject(infoList[nextIndex], nextIndex);
                        obj.transform.SetAsLastSibling();

						mNewAnchoredPosition = lowestRect.anchoredPosition;
						mNewAnchoredPosition.y += -lowestObj.currentHeight - spacing;

                        obj.GetComponent<RectTransform>().anchoredPosition = mNewAnchoredPosition;
					}
                }
                else if (mScrollVelocity.y < 0)
                {
					var objPosY = lowestRect.anchoredPosition.y + mScrollRect.content.anchoredPosition.y;
					if(objPosY < -(mScrollRect.content.rect.height + lowestObj.currentHeight * 0.1f))
					{
						var nextIndex = highestObj.currentIndex - 1;
						if (nextIndex < 0) return;
						objectPool.Release(lowestObj);                  
                        var obj = objectPool.Collect();
                        obj.updateScrollObject(infoList[nextIndex], nextIndex);
                        obj.transform.SetAsFirstSibling();

						mNewAnchoredPosition = highestRect.anchoredPosition;
						mNewAnchoredPosition.y += obj.currentHeight + spacing;
                                                      
                        obj.GetComponent<RectTransform>().anchoredPosition = mNewAnchoredPosition;
					}
                }
			}
        }

		private void ApplyOffsetIfNeeded()
		{         
			if (mIsVertical && Mathf.Abs(mScrollRect.content.anchoredPosition.y) > CONTENT_OFFSET_FIXER_LIMIT)
            {
                var v = (mScrollRect.content.anchoredPosition.y > 0 ? -CONTENT_OFFSET_FIXER_LIMIT : CONTENT_OFFSET_FIXER_LIMIT);
                mScrollRect.content.anchoredPosition = new Vector2(mScrollRect.content.anchoredPosition.x, mScrollRect.content.anchoredPosition.y + v);
                RectTransform objRectTransform;
                Vector2 objAnchoredPos;
                objectPool.ForEach(x =>
                {
                    objRectTransform = x.GetComponent<RectTransform>();
                    objAnchoredPos.x = objRectTransform.anchoredPosition.x;
                    objAnchoredPos.y = objRectTransform.anchoredPosition.y - v;
                    objRectTransform.anchoredPosition = objAnchoredPos;
                });
            }

            if (mIsHorizontal && Mathf.Abs(mScrollRect.content.anchoredPosition.x) > CONTENT_OFFSET_FIXER_LIMIT)
            {
                var v = (mScrollRect.content.anchoredPosition.x > 0 ? -CONTENT_OFFSET_FIXER_LIMIT : CONTENT_OFFSET_FIXER_LIMIT);
                mScrollRect.content.anchoredPosition = new Vector2(mScrollRect.content.anchoredPosition.x + v, mScrollRect.content.anchoredPosition.y);
                RectTransform objRectTransform;
                Vector2 objAnchoredPos;
                objectPool.ForEach(x =>
                {
                    objRectTransform = x.GetComponent<RectTransform>();
                    objAnchoredPos.x = objRectTransform.anchoredPosition.x - v;
                    objAnchoredPos.y = objRectTransform.anchoredPosition.y;
                    objRectTransform.anchoredPosition = objAnchoredPos;
                });
            }
		}

		private bool LimitScroll()
        {
			var lowestObj = GetLowest();
			var lowestPos = lowestObj.GetComponent<RectTransform>().anchoredPosition;
            var highestObj = GetHighest();
			var highestPos = highestObj.GetComponent<RectTransform>().anchoredPosition;
			var contentPos = mScrollRect.content.anchoredPosition;

            if (mIsVertical)
            {
				if (highestObj.currentIndex == 0)
				{
					//Going Down
					var limit = mScrollRect.content.rect.height;
					var objPosY = contentPos.y + highestPos.y + spacing + limit;
                    
					if (objPosY < limit)
					{
						mScrollRect.StopMovement();                  
						mScrollRect.content.anchoredPosition = new Vector2(contentPos.x, contentPos.y + limit - objPosY);
						return true;
					}
				}
				if (lowestObj.currentIndex == infoList.Length - 1)
                {
                    //Going Up
					var objPosY = contentPos.y + lowestPos.y + mScrollRect.content.rect.height - spacing;
					var limit = lowestObj.currentHeight;
                    
                    if (objPosY > limit)
                    {
                        mScrollRect.StopMovement();                  
						mScrollRect.content.anchoredPosition = new Vector2(contentPos.x, contentPos.y + limit - objPosY);
                        return true;
                    }               
                }
            }
            else if (mIsHorizontal)
            {
				if (highestObj.currentIndex == infoList.Length - 1)
                {
                    //Going Left
					var objPosX = mScrollRect.content.anchoredPosition.x + highestPos.x + spacing + mScrollRect.content.rect.width;
					var limit = mScrollRect.content.rect.width;

                    if (objPosX < limit)
                    {
                        mScrollRect.StopMovement();
						mScrollRect.content.anchoredPosition = new Vector2(contentPos.x + limit - objPosX, contentPos.y);
                        return true;
                    }
                }
				if (lowestObj.currentIndex == 0)
                {
                    //Going Right
					var objPosX = mScrollRect.content.anchoredPosition.x + lowestPos.x + mScrollRect.content.rect.width - spacing;
					var limit = lowestObj.currentWidth;

                    if (objPosX > limit)
                    {
                        mScrollRect.StopMovement();
						mScrollRect.content.anchoredPosition = new Vector2(contentPos.x + limit - objPosX, contentPos.y);
                        return true;
                    }               
                }
            }
            
			return false;
        }

        public T1 GetLowest()
        {
            var min = float.MaxValue;
            T1 lowestObj = null;
            var objs = objectPool;

			foreach (var t in objs)
            {
				var rectTransform = t.GetComponent<RectTransform>().anchoredPosition;
                
                if (mIsVertical && rectTransform.y < min || mIsHorizontal && rectTransform.x < min)
                {
                    min = mIsVertical ? rectTransform.y : rectTransform.x;
					lowestObj = t;
                }
            }

            return lowestObj;
        }

        public T1 GetHighest()
        {
            var max = float.MinValue;
            T1 highestObj = null;
            var objs = objectPool;
            foreach (var t in objs)
            {
				var rectTransform = t.GetComponent<RectTransform>().anchoredPosition;

				if(mIsVertical && rectTransform.y > max || mIsHorizontal && rectTransform.x > max)
				{
					max = mIsVertical ? rectTransform.y : rectTransform.x;
					highestObj = t;
				}
            }

            return highestObj;
        }
    }
}
